using System;
using System.IO;
using MathNet.Numerics.Optimization;
using Math = GrapeCity.DataVisualization.TypeScript.Math;

namespace Projekt.pliki;

public class wykresy
{
    private int smin = 20;
    private int prevSek = 3;
    private int startdif = 15;
    private int endDif = 30;
    private string reportFileName;

    private string REPORT_HEAD =
        "pacjent id; file id; wiek; plec ; czas_min; wyprzedzenie; opoznienie; wydluzenie;  standrd_BHI_value; " +
        "standard_BHI_at; maximal_BHI_value; maximal_BHI_started_at; maximal_BHI_measured_at;  minimal_mean_val; minimal_mean_at; " +
        "BHI_at_maximal_mean_val; maximal_mean_val; maximal_mean_at; maximal_BHI_from_minimal_mean_val; mean_val_at_maximal_BHI_from_minimal_mean; " +
        "maximal_BHI_from_minimal_mean_at\n";
    
    private wykresy(int smin, int prevSek, int startdif, int endDif, string reportFileName)
    {
        this.smin = smin;
        this.prevSek = prevSek;
        this.startdif = startdif;
        this.endDif = endDif;
        this.reportFileName = "report.csv";
        this.InitReportFile();
    }

    private void setParams(int smin, int prevSek, int startdif, int endDif)
    {
        this.smin = smin;
        this.prevSek = prevSek;
        this.startdif = startdif;
        this.endDif = endDif;
    }

    // public double BHI(double tpocz, double cs)
    // {
    //     double vp = meanU(tpocz);
    //     double ve = meanU(tpocz + cs);
    //     return (Math.pow(10, 4) * (ve - vp)) / (cs * vp);
    // }
    
    

    public void InitReportFile()
    {
        if (!File.Exists(reportFileName))
        {
            using (StreamWriter writer = new StreamWriter(reportFileName, false, System.Text.Encoding.UTF8))
            {
                writer.Write(REPORT_HEAD);
            }
        }
    }
}