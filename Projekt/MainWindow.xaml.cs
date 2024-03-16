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
using MathNet.Numerics;
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

using System.Security.Cryptography;

namespace Projekt
{
    public partial class MainWindow
    {
        const double upperBound = 100;
        private string[] _fnames;
        private int _currentRowId = -1;
        private bool _isNext = true;
        private BitmapImage _brainImage = new BitmapImage(new Uri("images/brain.jpg", UriKind.RelativeOrAbsolute));
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
            
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(GlobalExceptionHandler);
        }
        
        private void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                notesTextBox.AppendText(ex.StackTrace + Environment.NewLine + "\n");
                notesTextBox.Foreground = Brushes.Red;
                LogExceptionToFile(ex);
                
                MessageBox.Show("Aplikacja napotkała nieobsłużony błąd. Kliknij OK, aby zamknąć aplikację.");

                Application.Current.Shutdown();
            }
        }

        private void Copy_notes(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(notesTextBox.Text))
            {
                Clipboard.SetText(notesTextBox.Text);
                MessageBox.Show("Notes copied to clipboard.", "Copy Notes", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No notes to copy.", "Copy Notes", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Multiselect = true;
                openFileDialog.Filter = "Pliki Excel (*.xls;*.xlsx)|*.xls;*.xlsx|Wszystkie pliki (*.*)|*.*";

                if (openFileDialog.ShowDialog() == true)
                {
                    List<string> validFileNames = new List<string>();

                    foreach (string fileName in openFileDialog.FileNames)
                    {
                        if (IsFileLocked(fileName))
                        {
                            MessageBox.Show($"Plik {Path.GetFileName(fileName)} jest obecnie otwarty w innym programie i nie zostanie dodany do tej aplikacji.");
                        }
                        else
                        {
                            validFileNames.Add(fileName);
                        }
                    }

                    if (validFileNames.Count > 0)
                    {
                        _fnames = validFileNames.ToArray();
                        fileList.Items.Clear();

                        if (_fnames.Length == 1)
                        {
                            fileList.Items.Add(Path.GetFileName(_fnames[0]));
                        }
                        else
                        {
                            foreach (string file in _fnames)
                            {
                                fileList.Items.Add(Path.GetFileName(file));
                            }
                        }

                        _isNext = true;
                        ShowNext();
                    }
                }
            }
            catch (Exception ex)
            {
                LogExceptionToFile(ex);
            }
        }
        
        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }
        
        private void LogExceptionToFile(Exception ex)
        {
            string exceptionLog = $"{DateTime.Now} - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";

            try
            {
                string logDirectory = "logs";
                string logFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logDirectory);

                if (!Directory.Exists(logFolderPath))
                {
                    Directory.CreateDirectory(logFolderPath);
                }

                string logFilePath = Path.Combine(logFolderPath, "exception_log.txt");
                File.AppendAllText(logFilePath, exceptionLog);
            }
            catch (Exception)
            {
                MessageBox.Show("Błąd podczas zapisywania wyjątku do pliku.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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

            string reportFileName = "";
            try
            {
                reportFileName = "classify_report.json";
            } catch (InvalidOperationException ex)
            {
                notesTextBox.AppendText(ex.StackTrace);
                LogExceptionToFile(ex);
            }
            
            if (_isNext)
            {
                notesTextBox.AppendText("rowId: " + _currentRowId + "\n");
                var fName = _fnames[_currentRowId];
                notesTextBox.AppendText("The file " + Path.GetFileName(fName) + " has been classified as " + level + " " + shape + "\n");
                using (StreamWriter writer = new StreamWriter(reportFileName, true, Encoding.GetEncoding("utf-8")))
                {
                    string repLine = PrepareJson(level, shape, stamp(fName));

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
                notesTextBox.AppendText("No file to be classfied" + "\n");
            }
            
            ShowNext();
        }

        private string PrepareJson(string level, string shape, string stamp)
        {
            string js = "{";
            js += "\"C\":" + "\"" + level + "\"" + ", \"S\":" + "\"" + shape + "\"" + ", \"ts\":" + _currentRowMeans.TS;
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
            js += _currentRowMeans.M.Last() + "],";
            js += "\"stamp\":\"" + stamp + "\"";
            js += "}";
            return js;
        }

        private string stamp(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                // ComputeHash - zwraca tablicę bajtów, więc musimy ją przekonwertować na string
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

                // Konwertowanie tablicy bajtów na zapis szesnastkowy
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        public void ShowNext()
        {
            if (!_isNext)
            {
                Debug.WriteLine("No file to be classified");
                notesTextBox.AppendText("No file to be classified" + "\n");
                return;
            }

            List<int> timingi = new List<int>();
            List<double> meanU = new List<double>();
            List<double> xnew = new List<double>();
            int tpocz = 0;
            _currentRowId += 1;

            Debug.WriteLine("current row is " + _currentRowId + " fNames contains " + _fnames.Length + " elements");
            notesTextBox.AppendText("current row is " + _currentRowId + " fNames contains " + _fnames.Length + " elements" + "\n");
            if (_currentRowId < _fnames.Length)
            {
                string path = _fnames[_currentRowId];
                var fName = path.Split("/").Last();
                notesTextBox.AppendText("Next file is " + Path.GetFileName(fName) + "\n");
                examine.Text = Path.GetFileName(fName);
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
                
                xnew = XNewValue(timingi);
                
                PlotChart(meanU, timingi, tpocz, xnew);
            } else {
                _isNext = false;
                picture.Source = _brainImage;
                ClearFileList();
            }
            
            _currentRowMeans.TS = tpocz;
            _currentRowMeans.T = timingi;
            _currentRowMeans.M = meanU;
        }

        public void PlotChart(List<double> meanU, List<int> timings, int tpocz, List<double> xnew)
        {
            PlotModel plotModel = new PlotModel() { Background = OxyColors.White };
            var xs = new List<double> { tpocz, tpocz + 30000 };

            string imagePath;

            var zeroSeries = new LineSeries
            {
                Title = "",
                LineStyle = LineStyle.Dash,
                Color = OxyColor.FromRgb(255, 255, 255),
            };

            var upperSeries = new LineSeries
            {
                Title = "",
                LineStyle = LineStyle.Dash,
                Color = OxyColor.FromRgb(255, 255, 255),
            };

            // Tworzenie serii danych dla meanU
            var meanUSeries = new LineSeries
            {
                Title = "input",
                MarkerType = MarkerType.Circle,
                Color = OxyColor.FromRgb(0, 0, 255)
            };
            var meanUsmoothSeries = new LineSeries
            {
                Title = "smooth",
                LineStyle = LineStyle.Dash,
                Color = OxyColor.FromRgb(255, 0, 0),
            };
            var standardSeries = new LineSeries
            {
                Title = "standard",
                MarkerType = MarkerType.Circle,
                MarkerSize = 8,
                Color = OxyColor.FromRgb(0, 255, 0),
            };

            zeroSeries.Points.Add(new DataPoint(timings[0], 0));
            upperSeries.Points.Add(new DataPoint(timings[0], upperBound));

            for (int i = 0; i < timings.Count; i++)
            {
                meanUSeries.Points.Add(new DataPoint(timings[i], cutToScale(meanU[i])));
            }
            
            var interpolation = Interpolation(timings, meanU);
            
            var xnewMeanUsmooth = xnew.Select(x => interpolation.Interpolate(x)).ToArray();
            for (int i = 0; i < xnew.Count; i++)
            {
                meanUsmoothSeries.Points.Add(new DataPoint(xnew[i], cutToScale(xnewMeanUsmooth[i])));
            }
            
            var xsMeanUsmooth = xs.Select(x => interpolation.Interpolate(x)).ToArray();
            for (int i = 0; i < xs.Count; i++)
            {
                standardSeries.Points.Add(new DataPoint(xs[i], cutToScale(xsMeanUsmooth[i]) )); 
            }

            plotModel.Series.Add(upperSeries);
            plotModel.Series.Add(zeroSeries);
            plotModel.Series.Add(meanUSeries);
            plotModel.Series.Add(meanUsmoothSeries);
            plotModel.Series.Add(standardSeries);
            
            
            var exporter = new PngExporter { Width = 900, Height = 800};
            try
            {
                imagePath = "tmpChart.png";
                exporter.ExportToFile(plotModel, imagePath);
                
            }
            catch (InvalidOperationException ex)
            {
                notesTextBox.AppendText(ex.StackTrace);
                LogExceptionToFile(ex);
            }

            BitmapSource bitmap = exporter.ExportToBitmap(plotModel);
            picture.Source = bitmap;
        }

        private double cutToScale(double x)
        {
            return Math.Min(Math.Max(x, 0), upperBound);
        }

        private List<double> XNewValue(List<int> timingi)
        {
            int t0 = timingi.First();
            int tEnd = timingi.Last();
            int size = tEnd - t0 + 10;

            var xnew = Enumerable.Range(0, size)
                .Select(i => t0 + (i / (double)(size - 10)) * (tEnd - t0))
                .ToList();

            return xnew;
        } 

        private int FindTpocz(IWorkbook workbook)
        {
            int tpocz = 0;

            ISheet sheet = workbook.GetSheetAt(0);
            for (int row = 10; row <= sheet.LastRowNum; row++)
            {
                IRow currentRow = sheet.GetRow(row);
                if (currentRow != null)
                {
                    foreach (ICell cell in currentRow)
                    {
                        if (cell != null && cell.CellType == CellType.String && cell.StringCellValue.ToLower() == "początek" )
                        {
                            ICell timingCell = currentRow.GetCell(0); 
                            if (timingCell != null)
                            {
                                string tpoczValue = timingCell.StringCellValue;
                                TimeSpan czas = TimeSpan.ParseExact(tpoczValue, "hh\\:mm\\:ss\\.ff", null);
                                tpocz = (int)czas.TotalMilliseconds;
                            }
                            break;
                        }
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
        
        private IInterpolation Interpolation(List<int> timings, List<double> meanU)
        {
            double[] xData = timings.Select(i => (double)i).ToArray();
            double[] yData = meanU.ToArray();

            IInterpolation interpolation = CubicSpline.InterpolateNatural(xData, yData);

            return interpolation;
        }

        private void ClearFileList()
        {
            _fnames = null;
            fileList.Items.Clear();
            _currentRowId = -1;
        }
    }
}