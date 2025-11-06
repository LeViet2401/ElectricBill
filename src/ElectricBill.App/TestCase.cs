using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElectricBill.App
{
    public record TestCase(decimal kWh, int houseHolds, int month, decimal expected);
}
