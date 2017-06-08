using System.Collections.Generic;

namespace RcsConverter
{
    class RCSFlow
    {
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string Route { get; set; }
        public List<RCSTicket> TicketList { get; set; }
        public RCSFlow()
        {
            TicketList = new List<RCSTicket>();
        }
    }
}
