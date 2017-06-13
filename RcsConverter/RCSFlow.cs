using System;
using System.Collections.Generic;

namespace RcsConverter
{
    enum RCSFRecordType
    {
        NotSet,
        Insert,
        Amend,
        Delete,
        ForDeletion
    }
    class RCSFlow : IEquatable<RCSFlow>, IComparable<RCSFlow>
    {
        public RCSFRecordType recordType { get; set; }
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string Route { get; set; }
        public List<RCSTicket> TicketList { get; set; }
        public string LookupKey => Route + Origin + Destination;

        public RCSFlow()
        {
            recordType = RCSFRecordType.NotSet;
            TicketList = new List<RCSTicket>();
        }
        public bool Equals(RCSFlow other)
        {
            return Origin == other.Origin && Destination == other.Destination && Route == other.Route;
        }
        public void SetRecordType(string s)
        {
            if (s == "I")
            {
                recordType = RCSFRecordType.Insert;
            }
            else if (s == "A")
            {
                recordType = RCSFRecordType.Amend;
            }
            else if (s == "D")
            {
                recordType = RCSFRecordType.Delete;
            }
            else
            {
                throw new Exception($"Invalid record type: {s}");
            }
        }

        public int CompareTo(RCSFlow other)
        {
            return LookupKey.CompareTo(other.LookupKey);
        }
    }
}
