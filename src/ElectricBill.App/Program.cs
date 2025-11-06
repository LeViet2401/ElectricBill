// See https://aka.ms/new-console-template for more information
using ElectricBill.App;
using System.Collections.Generic;
using System.Text.Json;

Console.WriteLine("Hello, World!");
ElectricBillCalculator calculator = new ElectricBillCalculator();
//BoundaryTests
//EquivalenceTests
//DecisionTests
string test = "EquivalenceTests";
var jsonPath = Path.Combine($"D:\\Project\\Learning\\VNU\\ElectricBill\\Test\\ElectricBill.Test\\TestData", $"{test}.json");
var json = File.ReadAllText(jsonPath);
var testCases = JsonSerializer.Deserialize<List<TestCase>>(json);
List<TestCase> newList = new List<TestCase>();
foreach (var t in testCases)
{
    var a = calculator.CalculateElectricBill(t.kWh, t.houseHolds, t.month);
    newList.Add(new TestCase(t.kWh, t.houseHolds, t.month, a));
}
string json2 = JsonSerializer.Serialize(newList);
File.WriteAllText($"D:\\Project\\Learning\\VNU\\ElectricBill\\Test\\ElectricBill.Test\\TestData\\{test}_result.json", json2);

Console.WriteLine("Done");
Console.ReadLine();