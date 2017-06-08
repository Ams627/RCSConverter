using System.Collections.Generic;

namespace RcsConverter
{
    class RCSTicket
    {
        public string TicketCode { get; set; }
        public List<RCSFF> FFList { get; set; }
        public RCSTicket()
        {
            FFList = new List<RCSFF>();
        }
    }
}
