using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace foni
{
    public class GameData
    {
        // Region that handles driver selection and race directory
        public long MAIN_START { get; set; }
        public long OFMT_START { get; set; }
        // Region that handles alert messages (e.g. "__ is out of the session")
        public long SECOND_START { get; set; }
        public long SEARCH_LIMIT { get; set; }

        public string[] Drivers { get; set; }
    }
}
