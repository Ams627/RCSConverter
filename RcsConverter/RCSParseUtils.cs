using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RcsConverter
{
    static class RCSParseUtils
    {
        public static string CheckDate(string sdate)
        {
            // check the date is in the form yymmdd:
            if (sdate.Length != 6)
            {
                throw new Exception("A date in an RCS file must be six characters long in the form yymmdd");
            }
            if (sdate.Any(c => !Char.IsDigit(c)))
            {
                throw new Exception("Invalid character in date");
            }
            DateTime tempDateTime;
            if (!DateTime.TryParseExact("20" + sdate,
                                        "yyyyMMdd",
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.None, out tempDateTime))
            {
                throw new Exception($"Invalid date {sdate}");
            }

            return sdate;
        }
    }
}
