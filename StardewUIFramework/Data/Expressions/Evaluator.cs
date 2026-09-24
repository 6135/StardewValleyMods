using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace UIFramework.Data.Expressions
{
    /// <summary>
    /// Per-evaluation state handed to built-in and registered functions: the scope, the step budget, the volatile
    /// flag and an argument stack reused across calls (so calls don't allocate argument arrays).
    /// </summary>
    internal sealed class ExpressionContext
    {
        [ThreadStatic] private static Stack<ExpressionContext>? pool;

        private DataValue[] arguments = new DataValue[16];
        private int argumentTop;
        private int argumentHighWater;
        private int steps;

        private ExpressionContext()
        {
        }

        /// <summary>The scope being evaluated against (functions may cast it to the data layer's scope type).</summary>
        internal IExpressionScope Scope { get; private set; } = ExpressionScope.Empty;

        /// <summary>The function table in use.</summary>
        internal FunctionRegistry Functions { get; private set; } = FunctionRegistry.Default;

        /// <summary>True once anything volatile was read.</summary>
        internal bool IsVolatile { get; private set; }

        /// <summary>Flag the result as valid for the current tick only (e.g. hover state, GSQ results, game time).</summary>
        internal void MarkVolatile() => IsVolatile = true;

        /// <summary>Spend <paramref name="count"/> steps of the budget; fails the evaluation when it runs out.</summary>
        internal void Consume(int count = 1)
        {
            steps += count;
            if (steps > ExpressionLimits.MaxSteps)
            {
                Fail($"Expression exceeded its budget of {ExpressionLimits.MaxSteps} steps.");
            }
        }

        /// <summary>Abort the evaluation with <paramref name="message"/> (reported as the result's error, never thrown to callers).</summary>
        [DoesNotReturn]
        internal void Fail(string message) => throw new ExpressionException(message);

        /// <summary>Fail when a string of <paramref name="length"/> characters would exceed <see cref="ExpressionLimits.MaxStringLength"/>.</summary>
        internal void CheckLength(long length)
        {
            if (length > ExpressionLimits.MaxStringLength)
            {
                Fail($"String result is longer than {ExpressionLimits.MaxStringLength} characters.");
            }
        }

        /// <summary>A string value, after checking its length.</summary>
        internal DataValue Text(string value)
        {
            CheckLength(value.Length);
            return DataValue.FromString(value);
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Argument stack
        // -------------------------------------------------------------------------------------------------------------

        internal int ArgumentTop => argumentTop;

        internal void PushArgument(in DataValue value)
        {
            if (argumentTop == arguments.Length)
            {
                Array.Resize(ref arguments, arguments.Length * 2);
            }

            arguments[argumentTop++] = value;
            argumentHighWater = Math.Max(argumentHighWater, argumentTop);
        }

        internal ReadOnlySpan<DataValue> ArgumentsFrom(int start) => new(arguments, start, argumentTop - start);

        internal void PopArguments(int start) => argumentTop = start;

        // -------------------------------------------------------------------------------------------------------------
        //  Pooling (evaluations can nest: a scope may evaluate another expression while resolving a path)
        // -------------------------------------------------------------------------------------------------------------

        internal static ExpressionContext Rent(IExpressionScope scope, FunctionRegistry functions)
        {
            ExpressionContext context = pool != null && pool.Count > 0 ? pool.Pop() : new ExpressionContext();
            context.Scope = scope;
            context.Functions = functions;
            return context;
        }

        internal static void Return(ExpressionContext context)
        {
            Array.Clear(context.arguments, 0, context.argumentHighWater);
            context.argumentTop = 0;
            context.argumentHighWater = 0;
            context.steps = 0;
            context.IsVolatile = false;
            context.Scope = ExpressionScope.Empty;
            (pool ??= new Stack<ExpressionContext>()).Push(context);
        }
    }

    /// <summary>Internal control flow for evaluation errors; always caught by <see cref="Evaluator.Run"/>.</summary>
    internal sealed class ExpressionException : Exception
    {
        internal ExpressionException(string message) : base(message)
        {
        }
    }

    /// <summary>Tree-walking evaluator. Sandboxed: it only reads through the scope and calls registered functions.</summary>
    internal static class Evaluator
    {
        /// <summary>Evaluate <paramref name="root"/>; every failure (including exceptions from scopes and functions) becomes an error result.</summary>
        internal static ExpressionResult Run(Node root, IExpressionScope scope, FunctionRegistry functions)
        {
            ExpressionContext context = ExpressionContext.Rent(scope, functions);
            try
            {
                DataValue value = Evaluate(root, context);
                return new ExpressionResult(value, context.IsVolatile);
            }
            catch (ExpressionException ex)
            {
                return ExpressionResult.Failure(ex.Message, context.IsVolatile);
            }
            catch (Exception ex)
            {
                return ExpressionResult.Failure("Expression failed: " + ex.Message, context.IsVolatile);
            }
            finally
            {
                ExpressionContext.Return(context);
            }
        }

        private static DataValue Evaluate(Node node, ExpressionContext context)
        {
            context.Consume();
            switch (node.Kind)
            {
                case NodeKind.Literal:
                    return ((LiteralNode)node).Value;

                case NodeKind.Path:
                    return EvaluatePath((PathNode)node, context);

                case NodeKind.Member:
                    return EvaluateMember((MemberNode)node, context);

                case NodeKind.Unary:
                {
                    UnaryNode unary = (UnaryNode)node;
                    DataValue operand = Evaluate(unary.Operand, context);
                    return unary.Operator == UnaryOperator.Not
                        ? DataValue.FromBool(!operand.AsBool())
                        : DataValue.FromNumber(-operand.AsNumber());
                }

                case NodeKind.Binary:
                    return EvaluateBinary((BinaryNode)node, context);

                case NodeKind.Conditional:
                {
                    ConditionalNode conditional = (ConditionalNode)node;
                    return Evaluate(conditional.Condition, context).AsBool()
                        ? Evaluate(conditional.WhenTrue, context)
                        : Evaluate(conditional.WhenFalse, context);
                }

                case NodeKind.Call:
                    return EvaluateCall((CallNode)node, context);

                case NodeKind.ExternalCall:
                    return EvaluateExternalCall((ExternalCallNode)node, context);

                default:
                    context.Fail($"Unsupported expression node '{node.Kind}'.");
                    return DataValue.Null;
            }
        }

        private static DataValue EvaluatePath(PathNode node, ExpressionContext context)
        {
            PathSegment[]? path = node.StaticPath;
            if (path == null)
            {
                path = new PathSegment[node.Parts.Length];
                for (int i = 0; i < path.Length; i++)
                {
                    PathPart part = node.Parts[i];
                    string key = part.Key ?? Evaluate(part.Index!, context).AsString();
                    path[i] = new PathSegment(key, part.IsIndex);
                }
            }

            if (context.Scope.TryResolve(path, out DataValue value, out bool isVolatile))
            {
                if (isVolatile)
                {
                    context.MarkVolatile();
                }

                return value;
            }

            if (isVolatile)
            {
                context.MarkVolatile();
            }

            return DataValue.Null;
        }

        private static DataValue EvaluateMember(MemberNode node, ExpressionContext context)
        {
            DataValue target = Evaluate(node.Target, context);
            string key = node.Key ?? Evaluate(node.Index!, context).AsString();
            if (target.TryGetMember(key, out DataValue value))
            {
                return value;
            }

            if (target.Kind == DataKind.Object && context.Scope.TryGetMember(target, new PathSegment(key, node.IsIndex), out value, out bool isVolatile))
            {
                if (isVolatile)
                {
                    context.MarkVolatile();
                }

                return value;
            }

            return DataValue.Null;
        }

        private static DataValue EvaluateBinary(BinaryNode node, ExpressionContext context)
        {
            // short-circuit operators evaluate the right side only when needed
            if (node.Operator == BinaryOperator.And)
            {
                return DataValue.FromBool(Evaluate(node.Left, context).AsBool() && Evaluate(node.Right, context).AsBool());
            }

            if (node.Operator == BinaryOperator.Or)
            {
                return DataValue.FromBool(Evaluate(node.Left, context).AsBool() || Evaluate(node.Right, context).AsBool());
            }

            DataValue left = Evaluate(node.Left, context);
            DataValue right = Evaluate(node.Right, context);
            switch (node.Operator)
            {
                case BinaryOperator.Add:
                    if (left.Kind == DataKind.String || right.Kind == DataKind.String)
                    {
                        string a = left.AsString();
                        string b = right.AsString();
                        context.CheckLength((long)a.Length + b.Length);
                        return DataValue.FromString(string.Concat(a, b));
                    }

                    return DataValue.FromNumber(left.AsNumber() + right.AsNumber());

                case BinaryOperator.Subtract:
                    return DataValue.FromNumber(left.AsNumber() - right.AsNumber());

                case BinaryOperator.Multiply:
                    return DataValue.FromNumber(left.AsNumber() * right.AsNumber());

                case BinaryOperator.Divide:
                {
                    double divisor = right.AsNumber();
                    return divisor == 0 ? DataValue.Zero : DataValue.FromNumber(left.AsNumber() / divisor);
                }

                case BinaryOperator.Modulo:
                {
                    double divisor = right.AsNumber();
                    return divisor == 0 ? DataValue.Zero : DataValue.FromNumber(left.AsNumber() % divisor);
                }

                case BinaryOperator.Less:
                    return DataValue.FromBool(DataValue.Compare(left, right) < 0);

                case BinaryOperator.LessEqual:
                    return DataValue.FromBool(DataValue.Compare(left, right) <= 0);

                case BinaryOperator.Greater:
                    return DataValue.FromBool(DataValue.Compare(left, right) > 0);

                case BinaryOperator.GreaterEqual:
                    return DataValue.FromBool(DataValue.Compare(left, right) >= 0);

                case BinaryOperator.Equal:
                    return DataValue.FromBool(DataValue.LooseEquals(left, right));

                case BinaryOperator.NotEqual:
                    return DataValue.FromBool(!DataValue.LooseEquals(left, right));

                default:
                    context.Fail($"Unsupported operator '{node.Operator}'.");
                    return DataValue.Null;
            }
        }

        private static DataValue EvaluateCall(CallNode node, ExpressionContext context)
        {
            FunctionRegistry registry = context.Functions;
            FunctionDefinition? function;
            if (ReferenceEquals(node.CachedRegistry, registry) && node.CachedVersion == registry.Version)
            {
                function = node.CachedFunction;
            }
            else
            {
                registry.TryGet(node.Name, out function);
                node.CachedRegistry = registry;
                node.CachedVersion = registry.Version;
                node.CachedFunction = function;
            }

            if (function == null)
            {
                context.Fail($"Unknown function '{node.Name}' (at position {node.Position}).");
            }

            int count = node.Arguments.Length;
            if (count < function.MinArguments || (function.MaxArguments >= 0 && count > function.MaxArguments))
            {
                context.Fail($"{function.Name}() takes {DescribeArity(function)}, got {count} (at position {node.Position}).");
            }

            int start = context.ArgumentTop;
            try
            {
                foreach (Node argument in node.Arguments)
                {
                    context.PushArgument(Evaluate(argument, context));
                }

                if (function.IsVolatile)
                {
                    context.MarkVolatile();
                }

                return function.Invoke(context, context.ArgumentsFrom(start));
            }
            catch (ExpressionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ExpressionException($"{function.Name}() failed: {ex.Message}");
            }
            finally
            {
                context.PopArguments(start);
            }
        }

        private static DataValue EvaluateExternalCall(ExternalCallNode node, ExpressionContext context)
        {
            int start = context.ArgumentTop;
            try
            {
                foreach (Node argument in node.Arguments)
                {
                    context.PushArgument(Evaluate(argument, context));
                }

                if (!context.Scope.TryCallExternal(node.Name, context.ArgumentsFrom(start), out DataValue result, out bool isVolatile))
                {
                    context.Fail($"Unknown function '@{node.Name}' (at position {node.Position}).");
                }

                if (isVolatile)
                {
                    context.MarkVolatile();
                }

                if (result.Kind == DataKind.String)
                {
                    context.CheckLength(result.AsString().Length);
                }

                return result;
            }
            catch (ExpressionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ExpressionException($"@{node.Name}() failed: {ex.Message}");
            }
            finally
            {
                context.PopArguments(start);
            }
        }

        private static string DescribeArity(FunctionDefinition function)
        {
            if (function.MaxArguments < 0)
            {
                return $"at least {function.MinArguments} argument(s)";
            }

            return function.MinArguments == function.MaxArguments
                ? $"{function.MinArguments} argument(s)"
                : $"{function.MinArguments} to {function.MaxArguments} arguments";
        }
    }
}
