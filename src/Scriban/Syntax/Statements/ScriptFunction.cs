// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System;
using Scriban.Runtime;

namespace Scriban.Syntax
{
    [ScriptSyntax("function statement", "func <variable> ... end")]
    public partial class ScriptFunction : ScriptStatement, IScriptCustomFunction
    {
        private ScriptKeyword _funcToken;
        private ScriptNode _nameOrDoToken;
        private ScriptToken _openParen;
        private ScriptList<ScriptVariable> _parameters;
        private ScriptToken _closeParen;
        private ScriptToken _equalToken;
        private ScriptStatement _body;
        private bool _hasReturnType;

        public ScriptKeyword FuncToken
        {
            get => _funcToken;
            set => ParentToThis(ref _funcToken, value);
        }

        public ScriptNode NameOrDoToken
        {
            get => _nameOrDoToken;
            set
            {
                if (value != null && (!(value is ScriptVariable || (value is ScriptKeyword token && token.Value == "do"))))
                {
                    throw new ArgumentException($"Must be a {nameof(ScriptVariable)} or `do` {nameof(ScriptKeyword)}");
                }

                ParentToThis(ref _nameOrDoToken, value);
            }
        }

        public ScriptToken OpenParen
        {
            get => _openParen;
            set => ParentToThis(ref _openParen, value);
        }

        public ScriptList<ScriptVariable> Parameters
        {
            get => _parameters;
            set => ParentToThis(ref _parameters, value);
        }

        public ScriptToken CloseParen
        {
            get => _closeParen;
            set => ParentToThis(ref _closeParen, value);
        }

        public ScriptToken EqualToken
        {
            get => _equalToken;
            set => ParentToThis(ref _equalToken, value);
        }

        public ScriptStatement Body
        {
            get => _body;
            set
            {
                ParentToThis(ref _body, value);
                UpdateReturnType();
            }
        }

        public void UpdateReturnType()
        {
            _hasReturnType = Body is ScriptExpressionStatement || FindRetVisitor.HasRet(Body);
        }

        public bool IsAnonymous => !(NameOrDoToken is ScriptVariable);

        public bool HasParameters => Parameters != null;

        public override object Evaluate(TemplateContext context)
        {
            if (NameOrDoToken is ScriptVariable variable)
            {
                context.SetValue(variable, this);
                return null;
            }
            else
            {
                return this;
            }
        }

        public override bool CanHaveLeadingTrivia()
        {
            return NameOrDoToken != null;
        }

        public override void PrintTo(ScriptPrinter printer)
        {
            if (!IsAnonymous && Body is ScriptBlockStatement)
            {
                printer.Write(FuncToken).ExpectSpace();
            }
            printer.Write(NameOrDoToken);

            if (OpenParen != null) printer.Write(OpenParen);
            if (HasParameters)
            {
                if (OpenParen != null)
                {
                    printer.WriteListWithCommas(Parameters);
                }
                else
                {
                    for (var i = 0; i < Parameters.Count; i++)
                    {
                        var param = Parameters[i];
                        if (i > 0)
                        {
                            printer.ExpectSpace();
                        }
                        printer.Write(param);
                    }
                }
            }
            if (CloseParen != null) printer.Write(CloseParen);

            if (Body is ScriptBlockStatement)
            {
                printer.ExpectEos();
                printer.Write(Body);
            }
            else
            {
                printer.Write(EqualToken);
                printer.Write(Body);
            }
        }
        public object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
        {
            context.PushLocal();
            try
            {
                context.SetValue(ScriptVariable.Arguments, arguments, true);

                if (HasParameters)
                {
                    for (var i = 0; i < Parameters.Count; i++)
                    {
                        var arg = Parameters[i];
                        context.SetValue(arg, arguments[i]);
                    }
                }

                // Set the block delegate
                if (blockStatement != null)
                {
                    context.SetValue(ScriptVariable.BlockDelegate, blockStatement, true);
                }

                return context.Evaluate(Body);
            }
            finally
            {
                context.PopLocal();
            }
        }

        public int RequiredParameterCount => Parameters?.Count ?? 0;

        public int ParameterCount => Parameters?.Count ?? 0;

        public bool HasVariableParams => Parameters == null;

        public Type ReturnType => _hasReturnType ? typeof(object) : typeof(void);

        public ScriptParameterInfo GetParameterInfo(int index)
        {
            if (Parameters == null) return new ScriptParameterInfo(typeof(object), string.Empty);
            return new ScriptParameterInfo(typeof(object), Parameters[index].Name);
        }

        /// <summary>
        /// Finds a <see cref="ScriptReturnStatement"/> in a tree.
        /// TODO: could be provided as a generic version
        /// </summary>
        private class FindRetVisitor : ScriptVisitor
        {
            [ThreadStatic] private static FindRetVisitor _instance;

            private FindRetVisitor(){}

            public static bool HasRet(ScriptNode node)
            {
                if (node == null) return false;
                var local = _instance ??= new FindRetVisitor();
                local.Found = false;
                local.Visit(node);
                return local.Found;
            }

            public bool Found { get; private set; }

            public override void Visit(ScriptReturnStatement node)
            {
                Found = true;
            }

            protected override void DefaultVisit(ScriptNode node)
            {
                if (Found) return;

                if (node == null)
                    return;

                var childrenCount = node.ChildrenCount;
                for(int i = 0; i < childrenCount; i++)
                {
                    var child = node.GetChildren(i);
                    Visit(child);
                    if (Found) return; // early exit if found
                }
            }
        }
    }
}