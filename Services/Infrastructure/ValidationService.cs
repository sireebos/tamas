using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace RestaurantManager.Services.Infrastructure;

/// <summary>
/// Validation service for validating objects using Data Annotations
/// </summary>
public interface IValidationService
{
    bool TryValidate<T>(T obj, out List<ValidationResult> results) where T : class;
    List<ValidationResult> Validate<T>(T obj) where T : class;
    bool IsValid<T>(T obj) where T : class;
}

public class ValidationService : IValidationService
{
    public bool TryValidate<T>(T obj, out List<ValidationResult> results) where T : class
    {
        results = new List<ValidationResult>();
        var context = new ValidationContext(obj, serviceProvider: null, items: null);
        return Validator.TryValidateObject(obj, context, results, validateAllProperties: true);
    }

    public List<ValidationResult> Validate<T>(T obj) where T : class
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(obj, serviceProvider: null, items: null);
        Validator.TryValidateObject(obj, context, results, validateAllProperties: true);
        return results;
    }

    public bool IsValid<T>(T obj) where T : class
    {
        return TryValidate(obj, out _);
    }
}
