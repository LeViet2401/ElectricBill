// See https://aka.ms/new-console-template for more information
using ElectricBill.App;
using System.Collections.Generic;
using System.Text.Json;

Console.WriteLine("-------Hoa doan tinh tien dien-------");
ElectricBillCalculator calculator = new ElectricBillCalculator();
var bill = calculator.CalculateElectricBill(2147483647, 4, 8);
Console.WriteLine($"Tong so tien dien phai tra: {bill} VND");
Console.WriteLine("-------------------------------------");

Console.ReadLine();