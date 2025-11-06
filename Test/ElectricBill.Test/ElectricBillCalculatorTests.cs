using ElectricBill.App;
using FluentAssertions;
using System.Text.Json;

namespace ElectricBill.Test
{
    [TestFixture]
    public class ElectricBillCalculatorTests
    {
        private ElectricBillCalculator _calculator = null!;

        [SetUp]
        public void Setup()
        {
            _calculator = new ElectricBillCalculator();
        }

        public record TestCase(decimal kWh, int houseHolds, int month, decimal expected);

        private static IEnumerable<TestCaseData> LoadTestData(string fileName)
        {
            var jsonPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", fileName);
            var json = File.ReadAllText(jsonPath);
            var testCases = JsonSerializer.Deserialize<List<TestCase>>(json)!;

            foreach (var t in testCases)
            {
                yield return new TestCaseData(t.kWh, t.houseHolds, t.month, t.expected)
                    .SetName($"{Path.GetFileNameWithoutExtension(fileName)}_kWh={t.kWh},_houseHolds={t.houseHolds},_month={t.month}")
                    .SetCategory(Path.GetFileNameWithoutExtension(fileName));
            }
        }

        // ------------- 1️⃣ Kiểm thử biên ----------------
        [Test, TestCaseSource(nameof(BoundaryTestCases))]
        public void BoundaryValueTests(decimal kWh, int houseHolds, int month, decimal expected)
        {
            var result = _calculator.CalculateElectricBill(kWh, houseHolds, month);
            result.Should().Be(expected);
        }

        public static IEnumerable<TestCaseData> BoundaryTestCases =>
            LoadTestData("BoundaryTests.json");

        // ------------- 2️⃣ Kiểm thử tương đương ----------------
        [Test, TestCaseSource(nameof(EquivalenceTestCases))]
        public void EquivalenceClassTests(decimal kWh, int houseHolds, int month, decimal expected)
        {
            var result = _calculator.CalculateElectricBill(kWh, houseHolds, month);
            result.Should().Be(expected);
        }

        public static IEnumerable<TestCaseData> EquivalenceTestCases =>
            LoadTestData("EquivalenceTests.json");

        // ------------- 3️⃣ Kiểm thử bảng quyết định ----------------
        [Test, TestCaseSource(nameof(DecisionTestCases))]
        public void DecisionTableTests(decimal kWh, int houseHolds, int month, decimal expected)
        {
            var result = _calculator.CalculateElectricBill(kWh, houseHolds, month);
            result.Should().Be(expected);
        }

        public static IEnumerable<TestCaseData> DecisionTestCases =>
            LoadTestData("DecisionTests.json");
    }
}