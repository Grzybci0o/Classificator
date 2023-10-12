using System;
using System.Globalization;
using System.Text;

namespace Projekt;

public class tools
{
    static int sek = 100;
    static int min = 60 * sek;
    static int hour = 60 * min;

   public static int ParseTime(string s)
    {
        int t = 0;
        string[] ls = s.Split(":");
        t += hour * int.Parse(ls[0]);
        t += min * int.Parse(ls[1]);
        string[] seks = ls[2].Split(".");
        t += sek * int.Parse(seks[0]) + int.Parse(seks[1]);
        return t;
    }

    public static Tuple<int, int, int> ParseDate(string s)
    {
        try
        {
            string[] ls = s.Split("/");
            int y = int.Parse(ls[2]);
            int m = int.Parse(ls[0]);
            int d = int.Parse(ls[1]);
            return Tuple.Create(y, m, d);
        }
        catch
        {
            int y = DateTime.Now.Year;
            int m = DateTime.Now.Month;
            int d = DateTime.Now.Day;
            return Tuple.Create(y, m, d);
        }
    }

    public static Tuple<int, int, int> BirthDate(string pesel)
    {
        string ps = pesel.ToString();
        if (ps.Length > 6)
        {
            while (ps.Length < 11)
            {
                ps = "0" + ps;
            }
        }
        else
        {
            while (ps.Length < 6)
            {
                ps = "0" + ps;
            }
        }

        int yy = int.Parse(ps.Substring(0, 2)) + 1900;
        int mm = int.Parse(ps.Substring(2, 2));
        while (20 < mm)
        {
            mm -= 20;
            yy += 100;
        }

        int dd = int.Parse(ps.Substring(4, 2));
        return Tuple.Create(yy, mm, dd);
    }
}