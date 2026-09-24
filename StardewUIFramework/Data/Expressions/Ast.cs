using System;

namespace UIFramework.Data.Expressions
{
    /// <summary>Discriminator for <see cref="Node"/> subclasses (the evaluator switches on it).</summary>
    internal enum NodeKind : byte
    {
        Literal,
        Path,
        Member,
        Unary,
        Binary,
        Conditional,
        Call,
        ExternalCall
    }

    /// <summary>Unary operators.</summary>
    internal enum UnaryOperator : byte
    {
        Not,
        Negate
    }

    /// <summary>Binary operators, including the short-circuiting <see cref="And"/> / <see cref="Or"/>.</summary>
    internal enum BinaryOperator : byte
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        Less,
        LessEqual,
        Greater,
        GreaterEqual,
        Equal,
        NotEqual,
        And,
        Or
    }

    /// <summary>An immutable syntax-tree node. <see cref="Depth"/> is computed on construction so the parser can enforce <see cref="ExpressionLimits.MaxDepth"/>.</summary>
    internal abstract class Node
    {
        protected Node(NodeKind kind, int position, int depth, bool isConstant)
        {
            Kind = kind;
            Position = position;
            Depth = depth;
            IsConstant = isConstant;
        }

        internal NodeKind Kind { get; }

        /// <summary>Source offset used in error messages.</summary>
        internal int Position { get; }

        /// <summary>1 for leaves, otherwise 1 + the deepest child.</summary>
        internal int Depth { get; }

        /// <summary>True when the node reads no scope and calls no function (its value never changes).</summary>
        internal bool IsConstant { get; }

        protected static int DepthOf(Node a) => a.Depth + 1;

        protected static int DepthOf(Node a, Node b) => Math.Max(a.Depth, b.Depth) + 1;

        protected static int DepthOf(Node[] nodes)
        {
            int depth = 0;
            foreach (Node node in nodes)
            {
                depth = Math.Max(depth, node.Depth);
            }

            return depth + 1;
        }
    }

    /// <summary>A literal number, string, bool or null.</summary>
    internal sealed class LiteralNode : Node
    {
        internal LiteralNode(DataValue value, int position) : base(NodeKind.Literal, position, 1, true)
        {
            Value = value;
        }

        internal DataValue Value { get; }
    }

    /// <summary>One step of a <see cref="PathNode"/>: a static key, or an index expression evaluated at run time.</summary>
    internal readonly struct PathPart
    {
        internal PathPart(string key, bool isIndex)
        {
            Key = key;
            Index = null;
            IsIndex = isIndex;
        }

        internal PathPart(Node index)
        {
            Key = null;
            Index = index;
            IsIndex = true;
        }

        /// <summary>Static key (null when <see cref="Index"/> is set).</summary>
        internal string? Key { get; }

        /// <summary>Dynamic index expression (null for static keys).</summary>
        internal Node? Index { get; }

        /// <summary>True for <c>[key]</c>, false for <c>.key</c> and the root.</summary>
        internal bool IsIndex { get; }
    }

    /// <summary>
    /// A scope read such as <c>menu.x</c>, <c>menu[owner/menu].x</c>, <c>row.items[menu.i]</c> or <c>@owner/name.x</c>.
    /// The whole path is handed to <see cref="IExpressionScope.TryResolve"/> in one call.
    /// </summary>
    internal sealed class PathNode : Node
    {
        internal PathNode(PathPart[] parts, int position) : base(NodeKind.Path, position, ComputeDepth(parts), false)
        {
            Parts = parts;
            StaticPath = BuildStaticPath(parts);
        }

        /// <summary>Root first (<c>menu</c>, or <c>@owner/name</c> for references), then each member / index.</summary>
        internal PathPart[] Parts { get; }

        /// <summary>The ready-made segment array when no part is dynamic (reused on every evaluation), else null.</summary>
        internal PathSegment[]? StaticPath { get; }

        /// <summary>A new path with <paramref name="part"/> appended.</summary>
        internal PathNode Append(PathPart part)
        {
            PathPart[] parts = new PathPart[Parts.Length + 1];
            Array.Copy(Parts, parts, Parts.Length);
            parts[^1] = part;
            return new PathNode(parts, Position);
        }

        private static int ComputeDepth(PathPart[] parts)
        {
            int depth = 0;
            foreach (PathPart part in parts)
            {
                if (part.Index != null)
                {
                    depth = Math.Max(depth, part.Index.Depth);
                }
            }

            return depth + 1;
        }

        private static PathSegment[]? BuildStaticPath(PathPart[] parts)
        {
            PathSegment[] segments = new PathSegment[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Key == null)
                {
                    return null;
                }

                segments[i] = new PathSegment(parts[i].Key!, parts[i].IsIndex);
            }

            return segments;
        }
    }

    /// <summary>Member or index access on a computed value, e.g. <c>(a ? b : c).x</c> or <c>upper(s)[0]</c>.</summary>
    internal sealed class MemberNode : Node
    {
        internal MemberNode(Node target, string key, bool isIndex, int position) : base(NodeKind.Member, position, DepthOf(target), false)
        {
            Target = target;
            Key = key;
            IsIndex = isIndex;
        }

        internal MemberNode(Node target, Node index, int position) : base(NodeKind.Member, position, DepthOf(target, index), false)
        {
            Target = target;
            Index = index;
            IsIndex = true;
        }

        internal Node Target { get; }

        internal string? Key { get; }

        internal Node? Index { get; }

        internal bool IsIndex { get; }
    }

    /// <summary><c>!x</c> or <c>-x</c>.</summary>
    internal sealed class UnaryNode : Node
    {
        internal UnaryNode(UnaryOperator op, Node operand, int position) : base(NodeKind.Unary, position, DepthOf(operand), operand.IsConstant)
        {
            Operator = op;
            Operand = operand;
        }

        internal UnaryOperator Operator { get; }

        internal Node Operand { get; }
    }

    /// <summary>A binary operator (arithmetic, comparison, equality, or short-circuit logic).</summary>
    internal sealed class BinaryNode : Node
    {
        internal BinaryNode(BinaryOperator op, Node left, Node right, int position)
            : base(NodeKind.Binary, position, DepthOf(left, right), left.IsConstant && right.IsConstant)
        {
            Operator = op;
            Left = left;
            Right = right;
        }

        internal BinaryOperator Operator { get; }

        internal Node Left { get; }

        internal Node Right { get; }
    }

    /// <summary><c>condition ? whenTrue : whenFalse</c> (only the chosen branch is evaluated).</summary>
    internal sealed class ConditionalNode : Node
    {
        internal ConditionalNode(Node condition, Node whenTrue, Node whenFalse, int position)
            : base(NodeKind.Conditional, position, Math.Max(condition.Depth, Math.Max(whenTrue.Depth, whenFalse.Depth)) + 1,
                condition.IsConstant && whenTrue.IsConstant && whenFalse.IsConstant)
        {
            Condition = condition;
            WhenTrue = whenTrue;
            WhenFalse = whenFalse;
        }

        internal Node Condition { get; }

        internal Node WhenTrue { get; }

        internal Node WhenFalse { get; }
    }

    /// <summary>A call to a function from the <see cref="FunctionRegistry"/>, e.g. <c>round(x, 2)</c>.</summary>
    internal sealed class CallNode : Node
    {
        internal CallNode(string name, Node[] arguments, int position) : base(NodeKind.Call, position, DepthOf(arguments), false)
        {
            Name = name;
            Arguments = arguments;
        }

        internal string Name { get; }

        internal Node[] Arguments { get; }

        // lookup cache (single-threaded game loop; a registry change bumps its version and invalidates this)
        internal FunctionRegistry? CachedRegistry;
        internal int CachedVersion;
        internal FunctionDefinition? CachedFunction;
    }

    /// <summary>A call to a C#-exposed function on the scope, e.g. <c>@owner/name(1, 2)</c>.</summary>
    internal sealed class ExternalCallNode : Node
    {
        internal ExternalCallNode(string name, Node[] arguments, int position) : base(NodeKind.ExternalCall, position, DepthOf(arguments), false)
        {
            Name = name;
            Arguments = arguments;
        }

        /// <summary>The reference without the <c>@</c>, e.g. <c>owner/name</c>.</summary>
        internal string Name { get; }

        internal Node[] Arguments { get; }
    }
}
