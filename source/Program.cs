//
// Formula One Name Interchanger

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;

namespace foni
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //args = new string[] { "names.txt" };
            new Foni("F1_2021_dx12").Run(args);
        }
    }
}