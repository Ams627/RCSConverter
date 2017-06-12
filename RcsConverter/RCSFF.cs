using System;

namespace RcsConverter
{
    class RCSFF : IEquatable<RCSFF>
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string QuoteDate { get; set; }
        public string SeasonIndicator { get; set; }
        public string Key { get; set; }

        public string SortKey => StartDate + EndDate + SeasonIndicator;

        public bool Equals(RCSFF other)
        {
            var result = (SortKey == other.SortKey && QuoteDate == other.QuoteDate && SeasonIndicator == other.SeasonIndicator && Key == other.Key);
            return result;
        }
    }
}
