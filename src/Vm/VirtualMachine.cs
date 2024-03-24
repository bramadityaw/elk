using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting;
using Elk.Interpreting.Scope;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Vm;

class VirtualMachine
{
    private readonly FunctionTable _functions = new();
    private readonly IndexableStack<RuntimeObject> _stack = new();
    private readonly Dictionary<VariableSymbol, WeakReference<RuntimeObject>> _variables = new();
    private readonly InstructionExecutor _executor;
    private readonly InstructionGenerator _generator;
    private readonly ShellEnvironment _shellEnvironment = new();

    public VirtualMachine()
    {
        _executor = new InstructionExecutor(_stack, _variables);
        _generator = new InstructionGenerator(_functions, _executor, _shellEnvironment);
    }

    public Page Generate(Ast ast)
    {
        var page = _generator.Generate(ast);

        // TODO: This is just for debugging. Create a command line flag for this
        foreach (var function in ast.Expressions.Where(x => x is FunctionExpr).Cast<FunctionExpr>())
        {
            var symbol = function.Module.FindFunction(function.Identifier.Value, lookInImports: false)!;
            var functionPage = _functions.Get(symbol);
            Console.Write($"Page {functionPage.GetHashCode()} [{function.Identifier.Value}]");
            functionPage.Dump();
            Console.WriteLine();
        }

        return page;
    }

    public RuntimeObject Execute(Page page)
        => _executor.Execute(page);
}