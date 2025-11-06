using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            decimal baseAmount = CalculateBaseAmount(kWh, businessType);
            decimal businessMultiplier = GetBusinessMultiplier(businessType);
            decimal monthMultiplier = GetMonthMultiplier(month);


            var total = baseAmount * businessMultiplier * monthMultiplier;
            return Math.Round(total);
        }

        private decimal CalculateBaseAmount(decimal kWh, int businessType)
        {
            decimal total = 0;
            int previousLimit = 0;

            foreach (var (limit, price) in PriceTiers)
            {
                if (kWh > limit * businessType)
                {
                    total += (limit - previousLimit) * price;
                    previousLimit = limit * businessType;
                }
                else
                {
                    total += ((decimal)(kWh - previousLimit)) * price;
                    break;
                }
            }

            return total;
        }

        private decimal GetBusinessMultiplier(int businessType)
        {
            if (businessType >= 1 && businessType <= 5) return 1.0m;
            if (businessType >= 6 && businessType <= 15) return 1.2m;
            return 1.5m;
        }

        private decimal GetMonthMultiplier(int month)
        {
            if ((month >= 1 && month <= 3) || (month >= 7 && month <= 9)) return 1.0m;
            return 1.2m;
        }
    }
}
