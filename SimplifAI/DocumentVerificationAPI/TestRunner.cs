using DocumentVerificationAPI;

class TestRunner
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Database Integrity Test Runner ===");
        Console.WriteLine();
        
        try
        {
            await DatabaseIntegrityTest.RunTests();
            Console.WriteLine();
            Console.WriteLine("🎉 ALL TESTS PASSED! Database operations and data integrity verified successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ TEST FAILED: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}