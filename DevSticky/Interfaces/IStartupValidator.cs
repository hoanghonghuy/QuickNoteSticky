using System.Collections.Generic;
using System.Threading.Tasks;
using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for validating startup prerequisites and dependencies
/// </summary>
public interface IStartupValidator
{
    /// <summary>
    /// Validate all startup prerequisites
    /// </summary>
    /// <returns>Validation result containing all issues found</returns>
    ValidationResult Validate();
    
    /// <summary>
    /// Validate all startup prerequisites asynchronously
    /// </summary>
    /// <returns>Validation result containing all issues found</returns>
    Task<ValidationResult> ValidateAsync();
    
    /// <summary>
    /// Validate directories exist and are writable
    /// </summary>
    /// <returns>Validation result for directory checks</returns>
    ValidationResult ValidateDirectories();
    
    /// <summary>
    /// Validate services can be properly instantiated
    /// </summary>
    /// <returns>Validation result for service checks</returns>
    ValidationResult ValidateServices();
    
    /// <summary>
    /// Validate configuration files are accessible and well-formed
    /// </summary>
    /// <returns>Validation result for configuration checks</returns>
    ValidationResult ValidateConfiguration();
    
    /// <summary>
    /// Validate DI container dependencies can be resolved
    /// </summary>
    /// <returns>Validation result for DI container checks</returns>
    ValidationResult ValidateDependencyInjection();
    
    /// <summary>
    /// Validate resources (themes, dictionaries) are accessible
    /// </summary>
    /// <returns>Validation result for resource checks</returns>
    ValidationResult ValidateResources();
    
    /// <summary>
    /// Validate required dependencies (DLLs and NuGet packages) are available
    /// </summary>
    /// <returns>Validation result for dependency checks</returns>
    ValidationResult ValidateDependencies();
}