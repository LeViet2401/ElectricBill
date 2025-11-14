using Microsoft.CodeAnalysis.FlowAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElectricBill.App
{
    public class CfgPathFinder
    {
        // Danh sách các đường đi tìm được, mỗi đường đi là một danh sách các BasicBlock
        private readonly List<List<BasicBlock>> _allPaths = new List<List<BasicBlock>>();

        // Set dùng để phát hiện vòng lặp trong đường đi hiện tại (DFS)
        private readonly HashSet<BasicBlock> _visitedInCurrentPath = new HashSet<BasicBlock>();

        // Đường đi đang được khám phá
        private readonly List<BasicBlock> _currentPath = new List<BasicBlock>();

        /// <summary>
        /// Hàm chính để tìm tất cả các đường đi đơn (acyclic) trong CFG.
        /// </summary>
        public List<List<BasicBlock>> FindAllPaths(ControlFlowGraph cfg)
        {
            _allPaths.Clear();
            _visitedInCurrentPath.Clear();
            _currentPath.Clear();

            if (cfg.Blocks.Length == 0)
            {
                return _allPaths; // CFG rỗng
            }

            BasicBlock entryBlock = cfg.Blocks[0];

            DepthFirstSearch(entryBlock);

            return _allPaths;
        }

        /// <summary>
        /// Thuật toán tìm kiếm theo chiều sâu (DFS) để tìm đường đi.
        /// </summary>
        private void DepthFirstSearch(BasicBlock currentBlock)
        {
            // Thêm khối hiện tại vào đường đi và đánh dấu đã thăm
            _currentPath.Add(currentBlock);
            _visitedInCurrentPath.Add(currentBlock);

            bool isPathEnd = true;

            // SỬA LỖI: Thay thế 'Successors' bằng cách sử dụng 'FallThroughSuccessor' và 'ConditionalSuccessor'
            var successors = GetSuccessors(currentBlock);

            foreach (var successorBlock in successors)
            {
                isPathEnd = false;

                if (!_visitedInCurrentPath.Contains(successorBlock))
                {
                    DepthFirstSearch(successorBlock);
                }
            }

            if (isPathEnd || currentBlock.Kind == BasicBlockKind.Exit)
            {
                _allPaths.Add(new List<BasicBlock>(_currentPath));
            }

            _currentPath.RemoveAt(_currentPath.Count - 1);
            _visitedInCurrentPath.Remove(currentBlock);
        }

        /// <summary>
        /// Helper method to retrieve successors of a BasicBlock.
        /// </summary>
        private IEnumerable<BasicBlock> GetSuccessors(BasicBlock block)
        {
            var successors = new List<BasicBlock>();

            if (block.FallThroughSuccessor?.Destination != null)
            {
                successors.Add(block.FallThroughSuccessor.Destination);
            }

            if (block.ConditionalSuccessor?.Destination != null)
            {
                successors.Add(block.ConditionalSuccessor.Destination);
            }

            return successors;
        }

        public HashSet<BasicBlock> GetStatementBlocks(ControlFlowGraph cfg)
        {
            return cfg.Blocks
                .Where(b => b.Operations.Length > 0 && b.Kind == BasicBlockKind.Block)
                .ToHashSet();
        }

        public HashSet<(BasicBlock Source, BasicBlock Target)> GetBranchEdges(ControlFlowGraph cfg)
        {
            var edges = new HashSet<(BasicBlock, BasicBlock)>();
            foreach (var block in cfg.Blocks)
            {
                var conditionalBranches = block.ConditionalSuccessor != null
                    ? new[] { block.ConditionalSuccessor }
                    : Array.Empty<ControlFlowBranch>();

                foreach (var branch in conditionalBranches)
                {
                    if (branch?.Destination != null)
                    {
                        edges.Add((block, branch.Destination));
                    }
                }
            }
            return edges;
        }


        /// <summary>
        /// Thuật toán tham lam (Greedy) để tìm tập con tối thiểu cho Độ phủ Lệnh.
        /// </summary>
        public List<List<BasicBlock>> FindMinimalSetForStatementCoverage(
            List<List<BasicBlock>> allPaths,
            HashSet<BasicBlock> requiredBlocks)
        {
            var statementTestPaths = new List<List<BasicBlock>>();
            var uncoveredBlocks = new HashSet<BasicBlock>(requiredBlocks);

            // Chuyển các path thành các HashSet để tìm kiếm nhanh hơn
            var pathsAsBlockSets = allPaths
                .Select(path => new HashSet<BasicBlock>(path))
                .ToList();

            while (uncoveredBlocks.Count > 0)
            {
                HashSet<BasicBlock> bestPath = null;
                int maxNewBlocksCovered = 0;

                // Tìm đường đi "tốt nhất" (phủ được nhiều khối CHƯA ĐƯỢC PHỦ nhất)
                foreach (var path in pathsAsBlockSets)
                {
                    // Đếm số khối "mới" mà path này phủ được
                    int newBlocksCovered = path.Count(block => uncoveredBlocks.Contains(block));

                    if (newBlocksCovered > maxNewBlocksCovered)
                    {
                        maxNewBlocksCovered = newBlocksCovered;
                        bestPath = path;
                    }
                }

                // Nếu không tìm thấy path nào phủ thêm được (ví dụ: code không thể tới)
                if (bestPath == null || maxNewBlocksCovered == 0)
                {
                    Console.WriteLine($"[CẢNH BÁO] Không thể phủ {uncoveredBlocks.Count} khối lệnh còn lại.");
                    break; // Dừng lại
                }

                // Thêm path "tốt nhất" vào kết quả
                // (Chúng ta cần tìm lại path gốc dạng List, hơi tốn kém nhưng rõ ràng)
                var originalPath = allPaths.First(p => new HashSet<BasicBlock>(p).SetEquals(bestPath));
                statementTestPaths.Add(originalPath);

                // Xóa các khối đã được phủ khỏi tập "cần phủ"
                uncoveredBlocks.RemoveWhere(block => bestPath.Contains(block));

                // Xóa path này đi để không xét lại
                pathsAsBlockSets.Remove(bestPath);
            }

            return statementTestPaths;
        }

        /// <summary>
        /// Thuật toán tham lam (Greedy) để tìm tập con tối thiểu cho Độ phủ Nhánh.
        /// </summary>
        public List<List<BasicBlock>> FindMinimalSetForBranchCoverage(
            List<List<BasicBlock>> allPaths,
            HashSet<(BasicBlock Source, BasicBlock Target)> requiredEdges)
        {
            var branchTestPaths = new List<List<BasicBlock>>();
            var uncoveredEdges = new HashSet<(BasicBlock, BasicBlock)>(requiredEdges);

            // Tạo một cấu trúc dữ liệu để tra cứu nhanh: Path -> Các cạnh của nó
            var pathsWithEdges = allPaths.ToDictionary(
                path => path, // Key: Đường đi (List<BasicBlock>)
                path => GetEdgesInPath(path) // Value: Các cạnh của đường đi đó
            );

            while (uncoveredEdges.Count > 0)
            {
                List<BasicBlock> bestPath = null;
                int maxNewEdgesCovered = 0;

                // Tìm đường đi "tốt nhất" (phủ được nhiều cạnh CHƯA ĐƯỢC PHỦ nhất)
                foreach (var entry in pathsWithEdges)
                {
                    var path = entry.Key;
                    var edgesInThisPath = entry.Value;

                    int newEdgesCovered = edgesInThisPath.Count(edge => uncoveredEdges.Contains(edge));

                    if (newEdgesCovered > maxNewEdgesCovered)
                    {
                        maxNewEdgesCovered = newEdgesCovered;
                        bestPath = path;
                    }
                }

                if (bestPath == null || maxNewEdgesCovered == 0)
                {
                    Console.WriteLine($"[CẢNH BÁO] Không thể phủ {uncoveredEdges.Count} cạnh (nhánh) còn lại.");
                    break;
                }

                // Thêm path "tốt nhất" vào kết quả
                branchTestPaths.Add(bestPath);

                // Xóa các cạnh đã được phủ khỏi tập "cần phủ"
                var edgesInBestPath = pathsWithEdges[bestPath];
                uncoveredEdges.RemoveWhere(edge => edgesInBestPath.Contains(edge));

                // Xóa path này đi để không xét lại
                pathsWithEdges.Remove(bestPath);
            }

            return branchTestPaths;
        }

        /// <summary>
        /// Hàm tiện ích: Chuyển một đường đi (list các khối) thành 
        /// một tập hợp các cạnh (edge).
        /// </summary>
        private HashSet<(BasicBlock Source, BasicBlock Target)> GetEdgesInPath(
            List<BasicBlock> path)
        {
            var edges = new HashSet<(BasicBlock, BasicBlock)>();
            for (int i = 0; i < path.Count - 1; i++)
            {
                edges.Add((path[i], path[i + 1]));
            }
            return edges;
        }

        /// <summary>
        /// Hàm trợ giúp để in danh sách các đường đi
        /// </summary>
        public void PrintPaths(List<List<BasicBlock>> paths)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                var pathIds = paths[i].Select(b => $"B{b.Ordinal}");
                Console.WriteLine($"  Path {i + 1}: {string.Join(" -> ", pathIds)}");
            }
        }
    }
}

