using System;

namespace RcsConverter
{
    class RCSFF
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? QuoteDate { get; set; }
        public string SeasonIndicator { get; set; }
        public string Key { get; set; }
    }
}
