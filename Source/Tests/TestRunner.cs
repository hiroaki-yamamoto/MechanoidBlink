using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MechanoidBlink.Tests;

public static class TestRunner
{
    public static int Main(string[] args)
    {
        Console.WriteLine("=== Running MechanoidBlink Tests ===");
        var assembly = Assembly.GetExecutingAssembly();
        int passed = 0;
        int failed = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<TestClassAttribute>() == null)
            {
                continue;
            }

            Console.WriteLine($"\nRunning {type.Name}...");
            var testMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var initMethod = Array.Find(testMethods, m => m.GetCustomAttribute<TestInitializeAttribute>() != null);

            foreach (var method in testMethods)
            {
                if (method.GetCustomAttribute<TestMethodAttribute>() == null)
                {
                    continue;
                }

                object? instance = Activator.CreateInstance(type);
                try
                {
                    initMethod?.Invoke(instance, null);
                    method.Invoke(instance, null);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("  [PASS] ");
                    Console.ResetColor();
                    Console.WriteLine(method.Name);
                    passed++;
                }
                catch (TargetInvocationException ex)
                {
                    Exception root = ex;
                    while (root is TargetInvocationException tie && tie.InnerException is not null)
                    {
                        root = tie.InnerException;
                    }

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("  [FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"{method.Name}: {root.GetType().Name}: {root.Message}");
                    if (root.StackTrace is not null)
                    {
                        Console.WriteLine(root.StackTrace);
                    }
                    failed++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("  [FAIL] ");
                    Console.ResetColor();
                    Console.WriteLine($"{method.Name}: {ex.Message}");
                    failed++;
                }
            }
        }

        Console.WriteLine($"\n=== Test Results: {passed} Passed, {failed} Failed ===");
        return failed > 0 ? 1 : 0;
    }
}
