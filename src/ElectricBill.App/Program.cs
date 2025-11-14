// See https://aka.ms/new-console-template for more information
using ElectricBill.App;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.Json;

//Console.WriteLine("-------Hoa doan tinh tien dien-------");
//ElectricBillCalculator calculator = new ElectricBillCalculator();
//var bill = calculator.CalculateElectricBill(2147483647, 4, 8);
//Console.WriteLine($"Tong so tien dien phai tra: {bill} VND");
//Console.WriteLine("-------------------------------------");

var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ElectricBillCalculator.cs");
var cfgGenerator = new CFGGenerator(path);
(List<ControlFlowGraph> listcfg, SemanticModel semanticModel, List<MethodDeclarationSyntax> methodSyntaxs) = cfgGenerator.GenerateCFG(true);

CfgPathFinder pathFinder = new CfgPathFinder();

var inputParameters = methodSyntaxs.First().ParameterList.Parameters
            .Select(p => semanticModel.GetDeclaredSymbol(p))
            .ToList();
// --- 3. Tự động giải quyết từng Path bằng Z3 ---
var testInputGenerator = new TestInputGenerator(semanticModel);

foreach (var cfg in listcfg)
{
    Console.WriteLine($"\n>>> Processing CFG for method: {cfg.OriginalOperation}\n");
    var allPaths = pathFinder.FindAllPaths(cfg);
    int pathIndex = 1;
    foreach (var listpath in allPaths)
    {
        Console.WriteLine($"Path {pathIndex}:");
        foreach (var block in listpath)
        {
            Console.WriteLine($"  Block {block.Ordinal}:");
            foreach (var op in block.Operations)
            {
                Console.WriteLine($"    {op.Syntax}");
            }
        }
        pathIndex++;
        Console.WriteLine();
    }

    // === Phân tích Độ phủ Nhánh (Branch Coverage) ===
    var requiredEdges = pathFinder.GetBranchEdges(cfg);
    Console.WriteLine($"--- Độ phủ Nhánh (Branch Coverage) ---");
    Console.WriteLine($"Số cạnh (nhánh) cần phủ: {requiredEdges.Count}");
    foreach (var edge in requiredEdges)
    {
        Console.WriteLine($"  Cạnh: B{edge.Source.Ordinal} -> B{edge.Target.Ordinal}");
    }
    Console.WriteLine("-> Tập hợp các đường đi ở trên sẽ (hoặc nên) phủ tất cả các cạnh này.");
    Console.WriteLine();

    // === Phân tích Độ phủ Lệnh (Statement Coverage) ===
    var requiredBlocks = pathFinder.GetStatementBlocks(cfg);
    Console.WriteLine($"--- Độ phủ Lệnh (Statement Coverage) ---");
    Console.WriteLine($"Số khối (block) có lệnh cần phủ: {requiredBlocks.Count}");
    foreach (var block in requiredBlocks)
    {
        Console.WriteLine($"  Khối: B{block.Ordinal}");
    }
    Console.WriteLine("-> Tập hợp các đường đi ở trên sẽ (hoặc nên) phủ tất cả các khối này.");

    // === BƯỚC B: CHỌN TEST PATH CHO TỪNG ĐỘ PHỦ ===

    // B1: Độ phủ Đường (Path Coverage)
    Console.WriteLine($"\n--- 1. Test Paths cho Độ phủ Đường (All Paths) ---");
    Console.WriteLine($"Cần {allPaths.Count} test path:");
    pathFinder.PrintPaths(allPaths);

    // B2: Độ phủ Lệnh (Statement Coverage)
    List<List<BasicBlock>> statementTestPaths =
        pathFinder.FindMinimalSetForStatementCoverage(allPaths, requiredBlocks);

    Console.WriteLine($"\n--- 2. Test Paths cho Độ phủ Lệnh (Statement) ---");
    Console.WriteLine($"Cần {statementTestPaths.Count} test path (tối ưu):");
    pathFinder.PrintPaths(statementTestPaths);

    // B3: Độ phủ Nhánh (Branch Coverage)
    List<List<BasicBlock>> branchTestPaths =
        pathFinder.FindMinimalSetForBranchCoverage(allPaths, requiredEdges);

    Console.WriteLine($"\n--- 3. Test Paths cho Độ phủ Nhánh (Branch) ---");
    Console.WriteLine($"Cần {branchTestPaths.Count} test path (tối ưu):");
    pathFinder.PrintPaths(branchTestPaths);

    // --- C. TỰ ĐỘNG SINH INPUT CHO CÁC TEST PATH ---

    Console.WriteLine($"\n--- C. Tự động sinh Input cho các Test Path ---");
    var testInputsForAllPaths = testInputGenerator.GenerateInputsForPaths(allPaths, 
        semanticModel.GetDeclaredSymbol(methodSyntaxs.First()) as IMethodSymbol);
    testInputGenerator.SaveToJson(testInputsForAllPaths, "AllPathsInputs.json");
}







Console.WriteLine("\n✅ Done!");

Console.ReadLine();


