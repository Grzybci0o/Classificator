using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using MathNet.Numerics.Interpolation;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Wpf;
using Projekt.pliki;
using LineStyle = OxyPlot.LineStyle;

namespace Projekt
{
    public partial class MainWindow
    {
        private string[] _fnames;
        private int _currentRowId = -1;
        private bool _isNext = true;
        private BitmapImage _brainImage = new BitmapImage(new Uri("images/brain.jpg", UriKind.RelativeOrAbsolute));
        private BitmapImage bitmapImage;
        private currentRowMeans _currentRowMeans = new currentRowMeans();

        public MainWindow()
        {
            InitializeComponent();
            picture.Source = _brainImage;
            Thread.CurrentThread.CurrentCulture = 
                CultureInfo.GetCultureInfo("en");

            //Ustawienie cultury na en (mialem ',' zamiat '.' w double)
            LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "Pliki Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Wszystkie pliki (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                _fnames = openFileDialog.FileNames;
                fileList.Items.Clear();

                if (openFileDialog.FileNames.Length == 1)
                {
                    fileList.Items.Add(Path.GetFileName(_fnames[0]));
                }
                else
                {
                    foreach (string fileName in _fnames)
                    {
                        fileList.Items.Add(Path.GetFileName(fileName));
                    }
                }

                _currentRowId = 0;
            }

            if (_currentRowId == 0)
            {
                ShowNext();
            }
        }

        private void FaultNormalButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fnames != null)
            {
                ClassifyAs("fault", "normal");

            }
            else
            {
                MessageBox.Show("First add files!");
            }
        }

        private void FaultWaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fnames != null)
            {
                ClassifyAs("fault", "wave");
            }else
            {
                MessageBox.Show("First add files!");
            }
        }

        private void PerfectNormalButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fnames != null)
            {
                ClassifyAs("perfect", "normal");
            }else
            {
                MessageBox.Show("First add files!");
            }
        }

        private void PerfectWaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_fnames != null)
            {
                ClassifyAs("perfect", "wave");
            }else
            {
                MessageBox.Show("First add files!");
            }
        }

        private void ClassifyAs(string level, string shape)
        {
            string projectPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()))) ?? throw new InvalidOperationException(); // Ścieżka do katalogu projektu
            string plikiDirectory = Path.Combine(projectPath, "pliki");
            string reportFileName = Path.Combine(plikiDirectory, "classify_report.json");
            
            ShowNext();
            
            if (_isNext)
            {
                Debug.WriteLine("rowId: " + _currentRowId);
                var fName = _fnames[_currentRowId];
                Debug.WriteLine("The file " + fName + " has been classified as " + level + " " + shape);
                using (StreamWriter writer = new StreamWriter(reportFileName, true, Encoding.GetEncoding("utf-8")))
                {
                    string repLine = PrepareJson(level, shape);

                    if (_currentRowId < _fnames.Length)
                    {
                        repLine += "\n";
                    }

                    writer.Write(repLine);
                }
            }
            else
            {
                Debug.WriteLine("No file to be classfied");
            }
        }

        private string PrepareJson(string level, string shape)
        {
            string js = "{\"C\":" + "\"" + level + "\"" + ", \"S\":" + "\"" + shape + "\"" + ", \"ts\":" + _currentRowMeans.TS;
            js += ", \"T\":[";
            for (int i = 0; i < _currentRowMeans.T.Count - 1; i++)
            {
                js += _currentRowMeans.T[i] + ", ";
            }
            js += _currentRowMeans.T.Last() + "], \"M\":[";
            for (int i = 0; i < _currentRowMeans.M.Count - 1; i++)
            {
                js += _currentRowMeans.M[i] + ", ";
            }
            js += _currentRowMeans.M.Last() + "]}";
            return js;
        }

        public void ShowNext()
        {
            List<int> timingi = new List<int>();
            List<double> meanU = new List<double>();
            List<double> meanUSmooth = new List<double>();
            List<double> xnew = new List<double>();
            int tpocz = 0;
            _currentRowId += 1;
            
            Debug.WriteLine("current row is " + _currentRowId + " fNames contains " + _fnames.Length + " elements");
            if (_currentRowId < _fnames.Length)
            {
                string path = _fnames[_currentRowId];
                var fName = path.Split("/").Last();
                Debug.WriteLine("Next file is " + fName);
                using (var fs = new FileStream(fName, FileMode.Open, FileAccess.Read))
                {
                    IWorkbook workbook = null!;

                    // Wybieramy odpowiednią klasę w zależności od typu pliku Excela (XLSX lub XLS)
                    if (fName.EndsWith(".xlsx"))
                    {
                        workbook = new XSSFWorkbook(fs);
                    }
                    else if (fName.EndsWith(".xls"))
                    {
                        workbook = new HSSFWorkbook(fs);
                    }

                    FillMeanUAndTimingsLists(workbook, timingi, meanU);
                    tpocz = FindTpocz(workbook);
                }
                meanUSmooth = Interpolation(timingi,meanU);
                xnew = XNewValue(timingi);
                
                PlotChart(meanU, meanUSmooth, timingi, tpocz, xnew);
            } else {
                    _isNext = false;
                    picture.Source = _brainImage;
                    ClearFileList();
            }
            
            _currentRowMeans.TS = tpocz;
            _currentRowMeans.T = timingi;
            _currentRowMeans.M = meanU;
        }

        public void PlotChart(List<double> meanU, List<double> meanUsmooth, List<int> timings, int tpocz, List<double> xnew)
        {
            var plotModel = new PlotModel() { Background = OxyColors.White };
            var xs = new List<double> { tpocz, tpocz + 30000 };
            
            // Tworzenie serii danych dla meanU
            var meanUSeries = new LineSeries
            {
                Title = "input",
                MarkerType = MarkerType.Circle
            };

            for (int i = 0; i < timings.Count; i++)
            {
                meanUSeries.Points.Add(new DataPoint(timings[i], meanU[i]));
            }

            plotModel.Series.Add(meanUSeries);

            // Tworzenie serii danych dla meanUsmooth - do poprawy bo jest cos zle (obliczanie meanUsmooth)
            var meanUsmoothSeries = new LineSeries
            {
                Title = "smooth",
                LineStyle = LineStyle.Dash
            };

            for (int i = 0; i < xnew.Count; i++)
            {
                //interpolowane dane średnio tutaj działają (kropki wystrzeliwują możliwe, że funkcja interpolacji jest walnięta)
                meanUsmoothSeries.Points.Add(new DataPoint(xnew[i], meanUsmooth[i]));
            }
            
            plotModel.Series.Add(meanUsmoothSeries);

            // Tworzenie serii danych dla standard
            var standardSeries = new LineSeries
            {
                Title = "standard",
                MarkerType = MarkerType.Circle,
                MarkerSize = 8,
            };
            
            for (int i = 0; i < xs.Count; i++)
            {
                //interpolowane dane średnio tutaj działają (kropki wystrzeliwują możliwe, że funkcja interpolacji jest walnięta)
                standardSeries.Points.Add(new DataPoint(xs[i], meanUsmooth[i])); 
            }
            
            plotModel.Series.Add(standardSeries);
            
            var exporter = new PngExporter { Width = 640, Height = 480};

            // Zapisz wykres do pliku obrazu
            string projectPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory()))) ?? throw new InvalidOperationException(); // Ścieżka do katalogu projektu
            string imagesDirectory = Path.Combine(projectPath, "images");
            string imagePath = Path.Combine(imagesDirectory, "tmpChart.png");
            
            exporter.ExportToFile(plotModel, imagePath);
            
            // Utwórz kontrolkę OxyPlot
            var plotView = new PlotView();
            plotView.Model = plotModel;

            // Utwórz RenderTargetBitmap i ustaw rozmiar docelowy
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(640, 480, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(plotView);

            // Konwertuj RenderTargetBitmap na obraz
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
            using (var memoryStream = new MemoryStream())
            {
                encoder.Save(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                // Utwórz obraz na podstawie danych z RenderTargetBitmap
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();

                // Ustaw obraz jako źródło dla picture
                picture.Source = bitmapImage;
            }
        }

        private List<double> XNewValue(List<int> timingi)
        {
            int t0 = timingi.First();
            int tEnd = timingi.Last();
            var xnew = Enumerable.Range(0, timingi.Count)
                .Select(i => t0 + (i / (timingi.Count - 1.0)) * (tEnd - t0))
                .ToList();

            return xnew;
        } 

        private int FindTpocz(IWorkbook workbook)
        {
            int tpocz = 0;
            ISheet sheet = workbook.GetSheetAt(0);
            for (int row = 11; row <= sheet.LastRowNum; row++)
            {
                IRow currentRow = sheet.GetRow(row);
                if (currentRow != null)
                {
                    ICell tpoczCell = currentRow.GetCell(8);
                    ICell timingCell = currentRow.GetCell(0);
                    if (tpoczCell != null && tpoczCell.StringCellValue.Equals("początek"))
                    {
                        string tpoczValue = timingCell.StringCellValue;
                        TimeSpan czas = TimeSpan.ParseExact(tpoczValue, "hh\\:mm\\:ss\\.ff", null);
                        tpocz = (int)czas.TotalMilliseconds;
                    }
                }
            }
            return tpocz;
        }

        private void FillMeanUAndTimingsLists(IWorkbook workbook, List<int> timings, List<double> meanU)
        {
            ISheet sheet = workbook.GetSheetAt(0); // Zakładamy, że interesuje nas pierwszy arkusz

            // Przechodzimy przez wiersze od wiersza 11
            for (int row = 10; row <= sheet.LastRowNum; row++)
            {
                IRow currentRow = sheet.GetRow(row);
                if (currentRow != null)
                {
                    // Odczytanie wartości z kolumny 1 (indeks 0) i dodanie jej do listy timings
                    ICell timingsCell = currentRow.GetCell(0);
                    // Odczytanie wartości z kolumny 2 (indeks 1) i dodanie jej do listy meanU
                    ICell meanUCell = currentRow.GetCell(1);
                    if (timingsCell != null && meanUCell != null)
                    {
                        string timingsValue = timingsCell.StringCellValue;
                        TimeSpan czas = TimeSpan.ParseExact(timingsValue, "hh\\:mm\\:ss\\.ff", null);
                        int timingsInMs = (int)czas.TotalMilliseconds;
                        timings.Add(timingsInMs);
                    }
                    
                    if (meanUCell != null)
                    {
                        var meanUValue = meanUCell.NumericCellValue;
                        meanU.Add(meanUValue);
                    }
                }
            }
        }

        private List<double> Interpolation(List<int> timings, List<double> meanU)
        {
            double[] xData = Enumerable.Range(0, timings.Count).Select(i => (double)i).ToArray();
            double[] yData = meanU.ToArray();
            List<double> interpolationFunc = new List<double>();
            
            IInterpolation interpolation = CubicSpline.InterpolateNatural(xData, yData);

            foreach (var value in meanU)
            {
                double interpolatedValue = interpolation.Interpolate(value);
                interpolationFunc.Add(interpolatedValue);
            }

            return interpolationFunc;
        }

        private void ClearFileList()
        {
            _fnames = null;
            fileList.Items.Clear();
            _currentRowId = -1;
        }
    }
}