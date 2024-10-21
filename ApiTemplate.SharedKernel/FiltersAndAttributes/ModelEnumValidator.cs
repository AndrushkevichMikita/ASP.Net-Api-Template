using ApiTemplate.SharedKernel.ExceptionHandler;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections;

namespace ApiTemplate.SharedKernel.FiltersAndAttributes
{
    /// <summary>
    /// works globally
    /// /// </summary>
    public class ModelEnumValidatorAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Throws an error if the provided enum value is invalid.
        /// </summary>
        private static void ErrorIfEnumInvalid(Type enumType, object value, string propName)
        {
            if (value == null || !Enum.IsDefined(enumType, value))
            {
                var validValues = Enum.GetValues(enumType)
                                      .Cast<object>()
                                      .ToDictionary(t => (int)t, t => t.ToString());

                var validValuesMessage = string.Join(", ", validValues.Select(x => $"{x.Key}: {x.Value}"));
                throw new MyApplicationException(ErrorStatus.InvalidData,
                    $"Invalid value '{value ?? "null"}' for '{propName}'. Valid values: {validValuesMessage}");
            }
        }

        /// <summary>
        /// Validates a model, checking if any of its properties are invalid enums
        /// </summary>
        private static void ValidateModel(object model, string propName)
        {
            if (model == null) return;

            var modelType = model.GetType();

            // Skip strings, file types, and other special types
            if (IsString(modelType) || modelType.Name.Contains("FormFile") || modelType == typeof(Guid) || modelType == typeof(Uri)) return;

            // Only proceed if it's an enum, enumerable, or complex object
            if (modelType.IsEnum)
            {
                // If it's an enum, validate it directly
                ErrorIfEnumInvalid(modelType, model, propName);
            }
            else if (IsArrayOrEnumerable(modelType))
            {
                // Validates each item in an enumerable if it's an enum or a complex type.
                foreach (var item in (IEnumerable)model)
                {
                    ValidateModel(item, propName);
                }
            }
            else if (modelType.IsClass)
            {
                // Validates a complex type's properties recursively.
                foreach (var property in modelType.GetProperties())
                {
                    var propertyValue = property.GetValue(model);
                    ValidateModel(propertyValue, property.Name);
                }
            }
        }

        /// <summary>
        /// Checks if a type is a string
        /// </summary>
        private static bool IsString(Type type) => type == typeof(string);

        /// <summary>
        /// Determines if a type is an array or IEnumerable
        /// </summary>
        private static bool IsArrayOrEnumerable(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type) && !IsString(type);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Handle missing query parameters that are not nullable
            ValidateQueryParameters(context);

            // Validate model arguments
            foreach (var argument in context.ActionArguments)
            {
                var parameterDescriptor = context.ActionDescriptor.Parameters.FirstOrDefault(x => x.Name == argument.Key);
                if (parameterDescriptor != null && parameterDescriptor.BindingInfo?.BindingSource?.Id != "Services")
                {
                    ValidateModel(argument.Value, argument.Key);
                }
            }
        }

        /// <summary>
        /// Validates query parameters, ensuring non-nullable types are provided.
        /// </summary>
        private static void ValidateQueryParameters(ActionExecutingContext context)
        {
            if (context.ActionArguments.Count != context.ActionDescriptor.Parameters.Count)
            {
                foreach (var parameter in context.ActionDescriptor.Parameters)
                {
                    if (!context.ActionArguments.ContainsKey(parameter.Name) &&
                        Nullable.GetUnderlyingType(parameter.ParameterType) == null &&
                        parameter.ParameterType != typeof(string))
                    {
                        throw new MyApplicationException(ErrorStatus.InvalidData, $"Query parameter '{parameter.Name}' is required.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Convention to apply ModelEnumValidatorAttribute globally.
    /// </summary>
    public class ModelStateValidatorConvension : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controllerModel in application.Controllers)
            {
                controllerModel.Filters.Add(new ModelEnumValidatorAttribute());
            }
        }
    }
}
