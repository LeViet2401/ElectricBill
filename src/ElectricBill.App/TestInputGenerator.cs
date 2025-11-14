using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Z3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ElectricBill.App
{
    public class TestInputGenerator
    {
        private readonly Context _z3Context;
        private SemanticModel _semanticModel; // Assume this is set from Roslyn analysis

        public TestInputGenerator(SemanticModel semanticModel)
        {
            _z3Context = new Context();
            _semanticModel = semanticModel;
        }

        /// <summary>
        /// Generates inputs for each test path using Z3 solver.
        /// </summary>
        /// <param name="allPaths">List of test paths, each is List<BasicBlock> from DFS on CFG.</param>
        /// <param name="methodSymbol">The IMethodSymbol of the method being tested.</param>
        /// <returns>List of dictionaries with inputs.</returns>
        public List<Dictionary<string, object>> GenerateInputsForPaths(List<List<BasicBlock>> allPaths, IMethodSymbol methodSymbol)
        {
            var results = new List<Dictionary<string, object>>();

            // Symbolic variables
            var kWh = _z3Context.MkRealConst("kWh");
            var businessType = _z3Context.MkIntConst("businessType");
            var month = _z3Context.MkIntConst("month");

            // Initial symbolic state
            var symbolicState = new Dictionary<ISymbol, Expr>
            {
                { methodSymbol.Parameters[0], kWh },
                { methodSymbol.Parameters[1], businessType },
                { methodSymbol.Parameters[2], month }
            };

            for(int i = 0; i < allPaths.Count; i++)
            {
                using var solver = _z3Context.MkSolver();

                // Collect path constraints via symbolic execution
                var constraints = CollectPathConstraints(allPaths[i], symbolicState);

                solver.Add(constraints.ToArray());

                if (solver.Check() == Status.SATISFIABLE)
                {
                    var model = solver.Model;
                    var input = new Dictionary<string, object>
                    {
                        ["kWh"] = ParseReal(model.Eval(kWh, true)),
                        ["businessType"] = ParseInt(model.Eval(businessType, true)),
                        ["month"] = ParseInt(model.Eval(month, true))
                    };
                    results.Add(input);
                }
                else
                {
                    Console.WriteLine("Infeasible path");
                }
            }

            return results;
        }

        /// <summary>
        /// Performs symbolic execution along the path to collect constraints.
        /// </summary>
        /// <param name="path">The list of BasicBlocks in the path.</param>
        /// <param name="initialState">Initial symbolic state for parameters.</param>
        /// <returns>List of Z3 BoolExpr constraints.</returns>
        private List<BoolExpr> CollectPathConstraints(List<BasicBlock> path, Dictionary<ISymbol, Expr> initialState)
        {
            var constraints = new List<BoolExpr>();
            var state = new Dictionary<ISymbol, Expr>(initialState);
            var pathConditions = new Stack<BoolExpr>(); // For nested conditions if needed

            for (int i = 0; i < path.Count; i++)
            {
                var currentBlock = path[i];
                foreach (var operation in currentBlock.Operations)
                {
                    // Execute operation symbolically
                    ExecuteOperation(operation, state, constraints);
                }

                // Handle branch condition if not the last block
                if (i < path.Count - 1)
                {
                    var nextBlock = path[i + 1];
                    
                    var conditionalBranch = currentBlock.ConditionalSuccessor;
                    var fallThroughBranch = currentBlock.FallThroughSuccessor;
                    if (currentBlock.BranchValue != null)
                    {
                        var preBlock = path[i - 1];
                        // Get the condition expression
                        var conditionExpr = TranslateToZ3(currentBlock.BranchValue, state);
                        if (conditionExpr is BoolExpr boolCondition)
                        {
                            BoolExpr takenCondition;
                            // phep or
                            if(nextBlock.BranchValue == null && preBlock.BranchValue == null)
                            {
                                if (conditionalBranch != null && conditionalBranch.Destination == nextBlock)
                                {
                                    takenCondition = boolCondition; // Taken conditional (true) branch
                                }
                                else if (fallThroughBranch != null && fallThroughBranch.Destination == nextBlock)
                                {
                                    takenCondition = _z3Context.MkNot(boolCondition); // Taken fallthrough (false) branch
                                }
                                else
                                {
                                    // Handle other cases if needed
                                    continue;
                                }
                            }
                            else if (currentBlock.BranchValue != null && nextBlock.BranchValue != null)
                            {
                                if (currentBlock.ConditionKind == ControlFlowConditionKind.WhenTrue && nextBlock.ConditionKind == ControlFlowConditionKind.WhenFalse)
                                {
                                    if (conditionalBranch != null && conditionalBranch.Destination == nextBlock)
                                    {
                                        takenCondition = boolCondition; // Taken conditional (true) branch
                                    }
                                    else if (fallThroughBranch != null && fallThroughBranch.Destination == nextBlock)
                                    {
                                        takenCondition = _z3Context.MkNot(boolCondition); // Taken fallthrough (false) branch
                                    }
                                    else
                                    {
                                        // Handle other cases if needed
                                        continue;
                                    }
                                }
                                else
                                {
                                    if (conditionalBranch != null && conditionalBranch.Destination == nextBlock)
                                    {
                                        takenCondition = _z3Context.MkNot(boolCondition); // Taken fallthrough (false) branch
                                    }
                                    else if (fallThroughBranch != null && fallThroughBranch.Destination == nextBlock)
                                    {
                                        takenCondition = boolCondition;
                                    }
                                    else
                                    {
                                        // Handle other cases if needed
                                        continue;
                                    }
                                }
                            }
                            else if(preBlock.BranchValue != null && nextBlock.BranchValue == null)
                            {
                                if(preBlock.ConditionKind == ControlFlowConditionKind.WhenTrue && currentBlock.ConditionKind == ControlFlowConditionKind.WhenFalse)
                                {
                                    if (conditionalBranch != null && conditionalBranch.Destination == nextBlock)
                                    {
                                        takenCondition = _z3Context.MkNot(boolCondition); // Taken fallthrough (false) branch
                                    }
                                    else if (fallThroughBranch != null && fallThroughBranch.Destination == nextBlock)
                                    {
                                        takenCondition = boolCondition; // Taken conditional (true) branch
                                    }
                                    else
                                    {
                                        // Handle other cases if needed
                                        continue;
                                    }
                                }
                                else if (preBlock.ConditionKind == ControlFlowConditionKind.WhenFalse && currentBlock.ConditionKind == ControlFlowConditionKind.WhenFalse)
                                {
                                    if (conditionalBranch != null && conditionalBranch.Destination == nextBlock)
                                    {
                                        takenCondition = _z3Context.MkNot(boolCondition); // Taken fallthrough (false) branch

                                    }
                                    else if (fallThroughBranch != null && fallThroughBranch.Destination == nextBlock)
                                    {
                                        takenCondition = boolCondition; // Taken conditional (true) branch


                                    }
                                    else
                                    {
                                        // Handle other cases if needed
                                        continue;
                                    }
                                }
                                else
                                {
                                    if (conditionalBranch != null && conditionalBranch.Destination == nextBlock)
                                    {
                                        takenCondition = boolCondition; // Taken conditional (true) branch
                                    }
                                    else if (fallThroughBranch != null && fallThroughBranch.Destination == nextBlock)
                                    {
                                        takenCondition = _z3Context.MkNot(boolCondition); // Taken fallthrough (false) branch
                                    }
                                    else
                                    {
                                        // Handle other cases if needed
                                        continue;
                                    }
                                }
                            }
                            else
                            {
                                // Unsupported condition kind
                                continue;
                            }

                            constraints.Add(takenCondition);
                        }
                    }
                }
            }

            return constraints;
        }

        /// <summary>
        /// Symbolically executes an IOperation, updating state or adding constraints.
        /// </summary>
        private void ExecuteOperation(IOperation operation, Dictionary<ISymbol, Expr> state, List<BoolExpr> constraints)
        {
            switch (operation)
            {
                case IAssignmentOperation assignment:
                    var targetSymbol = GetSymbol(assignment.Target);
                    var valueExpr = TranslateToZ3(assignment.Value, state);
                    if (targetSymbol != null && valueExpr != null)
                    {
                        state[targetSymbol] = valueExpr;
                    }
                    break;

                case IVariableDeclarationOperation declaration:
                    foreach (var declarator in declaration.Declarators)
                    {
                        var localSymbol = declarator.Symbol;
                        if (localSymbol != null)
                        {
                            state[localSymbol] = CreateSymbolicVar(localSymbol);
                        }
                        if (declarator.Initializer?.Value != null)
                        {
                            var initExpr = TranslateToZ3(declarator.Initializer.Value, state);
                            state[localSymbol] = initExpr;
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Translates an IOperation to a Z3 Expr using current state.
        /// </summary>
        private Expr TranslateToZ3(IOperation op, Dictionary<ISymbol, Expr> state)
        {
            Expr operand;
            switch (op)
            {
                case ILiteralOperation literal:
                    if (literal.ConstantValue.HasValue)
                    {
                        var value = literal.ConstantValue.Value;
                        if (value is int intVal) return _z3Context.MkInt(intVal);
                        if (value is decimal decVal) return _z3Context.MkReal(decVal.ToString());
                        if (value is bool boolVal) return _z3Context.MkBool(boolVal);
                    }
                    break;

                case IParameterReferenceOperation paramRef:
                    var paramSymbol = paramRef.Parameter;
                    if (state.TryGetValue(paramSymbol, out var paramExpr))
                    {
                        return paramExpr;
                    }
                    break;

                case ILocalReferenceOperation localRef:
                    var localSymbol = localRef.Local;
                    if (state.TryGetValue(localSymbol, out var localExpr))
                    {
                        return localExpr;
                    }
                    break;

                case IFieldReferenceOperation fieldRef:
                    if (fieldRef.ConstantValue.HasValue)
                    {
                        var constValue = fieldRef.ConstantValue.Value;
                        if (constValue is int intVal) return _z3Context.MkInt(intVal);
                        // Add other types as needed
                    }
                    else
                    {
                        var fieldSymbol = fieldRef.Field;
                        if (state.TryGetValue(fieldSymbol, out var fieldExpr))
                        {
                            return fieldExpr;
                        }
                    }
                    break;

                case IBinaryOperation binary:
                    var left = TranslateToZ3(binary.LeftOperand, state);
                    var right = TranslateToZ3(binary.RightOperand, state);
                    if (left == null || right == null) return null;

                    bool isComparison = binary.OperatorKind == BinaryOperatorKind.Equals ||
                                        binary.OperatorKind == BinaryOperatorKind.NotEquals ||
                                        binary.OperatorKind == BinaryOperatorKind.GreaterThan ||
                                        binary.OperatorKind == BinaryOperatorKind.GreaterThanOrEqual ||
                                        binary.OperatorKind == BinaryOperatorKind.LessThan ||
                                        binary.OperatorKind == BinaryOperatorKind.LessThanOrEqual;

                    if (isComparison || binary.OperatorKind == BinaryOperatorKind.Add ||
                        binary.OperatorKind == BinaryOperatorKind.Subtract ||
                        binary.OperatorKind == BinaryOperatorKind.Multiply ||
                        binary.OperatorKind == BinaryOperatorKind.Divide)
                    {
                        (left, right) = CoerceToCommonArith(left, right);
                    }

                    return binary.OperatorKind switch
                    {
                        BinaryOperatorKind.Add => _z3Context.MkAdd((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.Subtract => _z3Context.MkSub((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.Multiply => _z3Context.MkMul((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.Divide => _z3Context.MkDiv((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.GreaterThan => _z3Context.MkGt((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.LessThan => _z3Context.MkLt((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.GreaterThanOrEqual => _z3Context.MkGe((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.LessThanOrEqual => _z3Context.MkLe((ArithExpr)left, (ArithExpr)right),
                        BinaryOperatorKind.Equals => _z3Context.MkEq(left, right),
                        BinaryOperatorKind.NotEquals => _z3Context.MkNot(_z3Context.MkEq(left, right)),
                        BinaryOperatorKind.And => _z3Context.MkAnd((BoolExpr)left, (BoolExpr)right),
                        BinaryOperatorKind.Or => _z3Context.MkOr((BoolExpr)left, (BoolExpr)right),
                        _ => null
                    };

                case IUnaryOperation unary:
                    operand = TranslateToZ3(unary.Operand, state);
                    if (operand == null) return null;

                    return unary.OperatorKind switch
                    {
                        UnaryOperatorKind.Not => _z3Context.MkNot((BoolExpr)operand),
                        _ => null
                    };

                case IInvocationOperation invocation:
                    if (invocation.TargetMethod.Name == "Round" && invocation.TargetMethod.ContainingType.Name == "Math")
                    {
                        return TranslateToZ3(invocation.Arguments[0].Value, state);
                    }
                    break;

                case IConversionOperation conversion:
                    operand = TranslateToZ3(conversion.Operand, state);
                    if (operand == null) return null;

                    if (conversion.Type.SpecialType == SpecialType.System_Decimal && operand is IntExpr intExpr)
                    {
                        return _z3Context.MkInt2Real(intExpr);
                    }

                    return operand;

                default:
                    return null;
            }
            return null;
        }

        private (Expr, Expr) CoerceToCommonArith(Expr left, Expr right)
        {
            if (left.Sort != right.Sort && left is ArithExpr && right is ArithExpr)
            {
                if (left is IntExpr && right is RealExpr)
                {
                    left = _z3Context.MkInt2Real((IntExpr)left);
                }
                else if (left is RealExpr && right is IntExpr)
                {
                    right = _z3Context.MkInt2Real((IntExpr)right);
                }
            }
            return (left, right);
        }

        private ISymbol GetSymbol(IOperation op)
        {
            return op switch
            {
                ILocalReferenceOperation local => local.Local,
                IParameterReferenceOperation param => param.Parameter,
                IFieldReferenceOperation field => field.Field,
                _ => null
            };
        }

        private Expr CreateSymbolicVar(ISymbol symbol)
        {
            ITypeSymbol typeSymbol = null;

            if (symbol is ILocalSymbol local)
            {
                typeSymbol = local.Type;
            }
            else if (symbol is IParameterSymbol param)
            {
                typeSymbol = param.Type;
            }
            else if (symbol is IFieldSymbol field)
            {
                typeSymbol = field.Type;
            }

            if (typeSymbol == null)
            {
                return null;
            }

            return typeSymbol.SpecialType switch
            {
                SpecialType.System_Int32 => _z3Context.MkIntConst(symbol.Name),
                SpecialType.System_Decimal => _z3Context.MkRealConst(symbol.Name),
                SpecialType.System_Boolean => _z3Context.MkBoolConst(symbol.Name),
                _ => null
            };
        }

        private decimal ParseReal(Expr expr)
        {
            if (expr is RatNum rat)
            {
                return decimal.Parse(rat.Numerator.ToString()) / decimal.Parse(rat.Denominator.ToString());
            }
            else if (expr is IntNum intNum)
            {
                return decimal.Parse(intNum.ToString());
            }
            else if (expr is FPNum fpNum)
            {
                return decimal.Parse(fpNum.ToString());
            }
            return decimal.Parse(expr.ToString());
        }

        private int ParseInt(Expr expr)
        {
            if (expr is IntNum intNum)
            {
                return int.Parse(intNum.ToString());
            }
            return int.Parse(expr.ToString());
        }

        public void SaveToJson(List<Dictionary<string, object>> inputs, string filePath)
        {
            var json = JsonSerializer.Serialize(inputs);
            System.IO.File.WriteAllText(filePath, json);
        }
    }
}
