using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace ElectricBill.App
{
    public class ElectricBillCalculator
    {
        private readonly (int limit, int price)[] PriceTiers =
        {
        (50, 1984),
        (100, 2050),
        (200, 2380),
        (300, 2998),
        (400, 3350),
        (int.MaxValue, 3460)
        };


        public decimal CalculateElectricBill(decimal kWh, int businessType, int month)
        {
            if (kWh < 0 || kWh > int.MaxValue)
                return -1;
            if (businessType < 1 || businessType > 100)
                return -1;
            if (month < 1 || month > 12)
                return -1;

            decimal baseAmount = 0;
            decimal businessMultiplier = 1;
            decimal monthMultiplier = 1;

            decimal previousLimit = 0;

            foreach (var (limit, price) in PriceTiers)
            {
                int newLimit = limit * businessType;
                if (limit > 400)
                {
                    newLimit = limit;
                }
                if (kWh > newLimit)
                {
                    baseAmount += (newLimit - previousLimit) * price;
                    previousLimit = newLimit;
                }
                else
                {
                    baseAmount += ((decimal)(kWh - previousLimit)) * price;
                    break;
                }
            }

            if (businessType <= 5 && businessType >= 1)
            {
                businessMultiplier = 1.0m;
            }
            else if (businessType <= 15 && businessType >= 6)
            {
                businessMultiplier = 1.2m;
            }
            else
            {
                businessMultiplier = 1.5m;
            }

            if ((month <= 6 && month >= 1))
            {
                monthMultiplier = 1.0m;
            }    
            else
            {
                monthMultiplier = 1.2m;
            }    

            var total = baseAmount * businessMultiplier * monthMultiplier;
            return Math.Round(total);
        }

    }
}
