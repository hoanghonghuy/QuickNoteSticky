using Xunit;
using Xunit.Abstractions;

namespace DevSticky.Tests
{
    public class TranslationReportTest
    {
        private readonly ITestOutputHelper _output;

        public TranslationReportTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GenerateTranslationReport()
        {
            // This test generates a detailed report about translation completeness
            // It will always pass, but outputs useful information
            
            var originalOut = System.Console.Out;
            try
            {
                using var writer = new System.IO.StringWriter();
                System.Console.SetOut(writer);
                
                TranslationVerificationReport.GenerateReport();
                
                var report = writer.ToString();
                _output.WriteLine(report);
            }
            finally
            {
                System.Console.SetOut(originalOut);
            }
        }
    }
}
