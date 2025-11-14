using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElectricBill.App
{
    public class CFGGenerator
    {
        private readonly string _filePath;

        public CFGGenerator(string filePath)
        {
            _filePath = filePath;
        }

        public (List<ControlFlowGraph>, SemanticModel, List<MethodDeclarationSyntax>) GenerateCFG(bool isSave)
        {
            if (!File.Exists(_filePath))
            {
                Console.WriteLine($"❌ File not found: {_filePath}");
                return (null, null, null);
            }
            List<ControlFlowGraph> cfgList = new List<ControlFlowGraph>();

            string code = File.ReadAllText(_filePath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var compilation = CSharpCompilation.Create("CFGAnalysis")
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
                .AddSyntaxTrees(tree);

            var model = compilation.GetSemanticModel(tree);

            // Lấy tất cả các phương thức
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();


            foreach (var method in methods)
            {
                Console.WriteLine($"\n🔹 Method: {method.Identifier.Text}");

                try
                {
                    var methodOp = model.GetOperation(method) as IMethodBodyOperation;
                    if (methodOp == null)
                    {
                        Console.WriteLine("⚠️ Cannot analyze method (no IMethodBodyOperation).");
                        continue;
                    }
                    ControlFlowGraph cfg = ControlFlowGraph.Create(methodOp);

                    if(isSave)
                    {
                        PrintCFG(cfg);
                        ExportCFGToGraphviz(cfg, method.Identifier.Text);
                    }    

                    cfgList.Add(cfg);


                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error analyzing method {method.Identifier.Text}: {ex.Message}");
                }
            }
            return (cfgList, model, methods);
        }

        private void PrintCFG(ControlFlowGraph cfg)
        {
            foreach (var block in cfg.Blocks)
            {
                Console.WriteLine($"  ▪ Block #{block.Ordinal} (Kind: {block.Kind}):");

                foreach (var op in block.Operations)
                    Console.WriteLine($"     {op.Syntax}");

                var successors = GetSuccessorsSafe(block);
                Console.WriteLine($"     → Successors: {(successors.Count > 0 ? string.Join(", ", successors) : "(none)")}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Lấy danh sách successor ordinal của block một cách an toàn,
        /// tránh NullReferenceException do getter của Roslyn có thể trả null/ném.
        /// </summary>
        private List<int> GetSuccessorsSafe(BasicBlock block)
        {
            var list = new List<int>();

            // Một số block (Entry / Exit) có thể không có successors, hoặc getter ném
            // Bọc từng truy xuất trong try/catch để an toàn.
            try
            {
                var cond = block.ConditionalSuccessor;
                if (cond != null && cond.Destination != null)
                    list.Add(cond.Destination.Ordinal);
            }
            catch (NullReferenceException)
            {
                // bỏ qua, vì vài getter trong Roslyn có thể trả null/throw
            }
            catch
            {
                // bỏ qua các lỗi bất ngờ ở đây để đảm bảo chương trình không dừng
            }

            try
            {
                var ft = block.FallThroughSuccessor;
                if (ft != null && ft.Destination != null)
                    list.Add(ft.Destination.Ordinal);
            }
            catch (NullReferenceException)
            {
            }
            catch
            {
            }

            // Có thể có duplicate nếu cả 2 trỏ tới cùng block, loại bỏ duplicate
            return list.Distinct().ToList();
        }

        private void ExportCFGToGraphviz(ControlFlowGraph cfg, string methodName)
        {
            string dotFile = $"{methodName}_cfg.dot";
            string pngFile = $"{methodName}_cfg.png";

            using (var writer = new StreamWriter(dotFile))
            {
                writer.WriteLine("digraph CFG {");
                writer.WriteLine("  node [shape=box, style=rounded, fontname=\"Consolas\", fontsize=10];");
                writer.WriteLine("  rankdir=TB;");

                foreach (var block in cfg.Blocks)
                {
                    var labelLines = new List<string>();

                    // Nếu có điều kiện (branch expression)
                    if (block.BranchValue != null)
                    {

                        labelLines.Add($"[Cond] {block.BranchValue.Syntax}".Replace("\"", "\\\""));

                        // --- 🧩 In ra cạnh ---
                        // Nếu có điều kiện, ta phân biệt True / False
                        if (block.ConditionalSuccessor?.Destination != null)
                            writer.WriteLine($"  B{block.Ordinal} -> B{block.ConditionalSuccessor.Destination.Ordinal} [label=\"True\"];");

                        if (block.FallThroughSuccessor?.Destination != null)
                            writer.WriteLine($"  B{block.Ordinal} -> B{block.FallThroughSuccessor.Destination.Ordinal} [label=\"False\"];");

                    }
                    else
                    {
                        // Câu lệnh trong block
                        foreach (var op in block.Operations)
                            labelLines.Add(op.Syntax.ToString().Replace("\"", "\\\""));
                        if (block.FallThroughSuccessor?.Destination != null)
                            writer.WriteLine($"  B{block.Ordinal} -> B{block.FallThroughSuccessor.Destination.Ordinal};");
                    }

                    string label = string.Join("\\n", labelLines);
                    if (string.IsNullOrWhiteSpace(label))
                        label = block.Kind.ToString();

                    writer.WriteLine($"  B{block.Ordinal} [label=\"B{block.Ordinal}: {label}\"];");

                }

                writer.WriteLine("}");
            }

            Console.WriteLine($"✅ Exported DOT: {dotFile}");

            SaveCFGImage(dotFile, pngFile);
        }

        private void SaveCFGImage(string dotFile, string pngFile)
        {
            try
            {
                string dotPath = @"C:\Program Files\Graphviz\bin\dot.exe";
                if (!File.Exists(dotPath))
                {
                    Console.WriteLine("❌ dot.exe not found at " + dotPath);
                    return;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = dotPath,
                        Arguments = $"-Tpng -Gdpi=300 \"{dotFile}\" -o \"{pngFile}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();

                Console.WriteLine("Graphviz output generated: " + pngFile);

                if (File.Exists(pngFile))
                    Console.WriteLine($"🖼️  Graphviz output: {pngFile}");
                else
                    Console.WriteLine("⚠️ Graphviz failed to produce PNG (check Graphviz PATH).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Graphviz execution failed: {ex.Message}");
            }
        }
    }
}
