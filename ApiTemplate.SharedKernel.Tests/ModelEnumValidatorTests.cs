using ApiTemplate.SharedKernel.ExceptionHandler;
using ApiTemplate.SharedKernel.FiltersAndAttributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace ApiTemplate.SharedKernel.Tests
{
    public enum TestEnum
    {
        Value1 = 1,
        Value2 = 2
    }

    public class ModelEnumValidatorTests
    {
        private readonly ModelEnumValidatorAttribute _validator;

        public ModelEnumValidatorTests()
        {
            _validator = new ModelEnumValidatorAttribute();
        }

        #region Enum Tests
        [Fact]
        public void OnActionExecuting_ValidEnum_DoesNotThrowException()
        {
            // Arrange
            var actionContext = CreateActionContextWithParameterValue("validEnum", TestEnum.Value1);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "validEnum", TestEnum.Value1 } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_InvalidEnum_ThrowsMyApplicationException()
        {
            // Arrange
            var actionContext = CreateActionContextWithParameterValue("invalidEnum", (TestEnum)999);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "invalidEnum", (TestEnum)999 } }, null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }
        #endregion

        #region Array of Enums Tests
        [Fact]
        public void OnActionExecuting_ValidEnumArray_DoesNotThrowException()
        {
            // Arrange
            var validEnumArray = new TestEnum[] { TestEnum.Value1, TestEnum.Value2 };
            var actionContext = CreateActionContextWithParameterValue("validEnumArray", validEnumArray);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "validEnumArray", validEnumArray } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_InvalidEnumInArray_ThrowsMyApplicationException()
        {
            // Arrange
            var invalidEnumArray = new TestEnum[] { TestEnum.Value1, (TestEnum)999 };
            var actionContext = CreateActionContextWithParameterValue("invalidEnumArray", invalidEnumArray);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "invalidEnumArray", invalidEnumArray } }, null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }
        #endregion

        #region Nullable Enum in Array Tests
        [Fact]
        public void OnActionExecuting_ArrayOfNullableEnumsWithValidValues_DoesNotThrowException()
        {
            // Arrange
            var nullableEnumArray = new TestEnum?[] { TestEnum.Value1, null, TestEnum.Value2 };
            var actionContext = CreateActionContextWithParameterValue("nullableEnumArray", nullableEnumArray);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "nullableEnumArray", nullableEnumArray } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_ArrayOfNullableEnumsWithInvalidValue_ThrowsMyApplicationException()
        {
            // Arrange
            var nullableEnumArray = new TestEnum?[] { TestEnum.Value1, (TestEnum?)999, null };
            var actionContext = CreateActionContextWithParameterValue("nullableEnumArray", nullableEnumArray);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "nullableEnumArray", nullableEnumArray } }, null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }
        #endregion

        #region Nullable Value Type in Array Tests

        [Fact]
        public void OnActionExecuting_ArrayOfNullableIntsWithValidValues_DoesNotThrowException()
        {
            // Arrange
            var nullableIntArray = new int?[] { 1, null, 2 };
            var actionContext = CreateActionContextWithParameterValue("nullableIntArray", nullableIntArray);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "nullableIntArray", nullableIntArray } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_ArrayOfNullableIntsWithAllNullValues_DoesNotThrowException()
        {
            // Arrange
            var nullableIntArray = new int?[] { null, null, null };
            var actionContext = CreateActionContextWithParameterValue("nullableIntArray", nullableIntArray);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "nullableIntArray", nullableIntArray } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        #endregion

        #region Nullable Enum Tests
        [Fact]
        public void OnActionExecuting_ValidNullableEnum_DoesNotThrowException()
        {
            // Arrange
            TestEnum? validNullableEnum = TestEnum.Value1;
            var actionContext = CreateActionContextWithParameterValue("validNullableEnum", validNullableEnum);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "validNullableEnum", validNullableEnum } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_InvalidNullableEnum_ThrowsMyApplicationException()
        {
            // Arrange
            TestEnum? invalidNullableEnum = (TestEnum)999;
            var actionContext = CreateActionContextWithParameterValue("invalidNullableEnum", invalidNullableEnum);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "invalidNullableEnum", invalidNullableEnum } }, null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }

        [Fact]
        public void OnActionExecuting_NullNullableEnum_DoesNotThrowException()
        {
            // Arrange
            TestEnum? nullNullableEnum = null;
            var actionContext = CreateActionContextWithParameterValue("nullNullableEnum", nullNullableEnum);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "nullNullableEnum", nullNullableEnum } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }
        #endregion

        #region Test Complex Objects via OnActionExecuting
        public class ComplexObject
        {
            public TestEnum EnumProperty { get; set; }
            public string StringProperty { get; set; }
        }

        public class ComplexObjectWithNullableFields
        {
            public TestEnum? NullableEnum { get; set; }
            public int? NullableInt { get; set; }
            public string StringProperty { get; set; }
        }

        [Fact]
        public void OnActionExecuting_ComplexObjectWithValidEnum_DoesNotThrowException()
        {
            // Arrange
            var complexObject = new ComplexObject { EnumProperty = TestEnum.Value1, StringProperty = "test" };
            var actionContext = CreateActionContextWithParameterValue("complexObject", complexObject);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "complexObject", complexObject } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_ComplexObjectWithInvalidEnum_ThrowsMyApplicationException()
        {
            // Arrange
            var complexObject = new ComplexObject { EnumProperty = (TestEnum)999, StringProperty = "test" };
            var actionContext = CreateActionContextWithParameterValue("complexObject", complexObject);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "complexObject", complexObject } }, null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }

        [Fact]
        public void OnActionExecuting_ComplexObjectWithValidNullableFields_DoesNotThrowException()
        {
            // Arrange
            var complexObject = new ComplexObjectWithNullableFields
            {
                NullableEnum = TestEnum.Value1,
                NullableInt = 42,
                StringProperty = "test"
            };
            var actionContext = CreateActionContextWithParameterValue("complexObject", complexObject);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "complexObject", complexObject } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_ComplexObjectWithInvalidNullableEnum_ThrowsMyApplicationException()
        {
            // Arrange
            var complexObject = new ComplexObjectWithNullableFields
            {
                NullableEnum = (TestEnum?)999,  // Invalid enum
                NullableInt = 42,
                StringProperty = "test"
            };
            var actionContext = CreateActionContextWithParameterValue("complexObject", complexObject);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "complexObject", complexObject } }, null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }

        [Fact]
        public void OnActionExecuting_ComplexObjectWithAllNullableFieldsNull_DoesNotThrowException()
        {
            // Arrange
            var complexObject = new ComplexObjectWithNullableFields
            {
                NullableEnum = null,
                NullableInt = null,
                StringProperty = "test"
            };
            var actionContext = CreateActionContextWithParameterValue("complexObject", complexObject);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "complexObject", complexObject } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }
        #endregion

        #region Test Query Parameter Validation
        [Fact]
        public void OnActionExecuting_MissingNonNullableQueryParameter_ThrowsMyApplicationException()
        {
            // Arrange
            var actionContext = CreateActionContext();
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object>(), null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }

        [Fact]
        public void OnActionExecuting_AllQueryParametersPresent_DoesNotThrowException()
        {
            // Arrange
            var actionContext = CreateActionContext();
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "requiredParam", 42 } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }
        #endregion

        #region Array of Complex Objects Tests
        [Fact]
        public void OnActionExecuting_ArrayOfComplexObjectsWithValidEnums_DoesNotThrowException()
        {
            // Arrange
            var complexObjects = new ComplexObject[]
            {
                new ComplexObject { EnumProperty = TestEnum.Value1, StringProperty = "test" },
                new ComplexObject { EnumProperty = TestEnum.Value2, StringProperty = "test" }
            };
            var actionContext = CreateActionContextWithParameterValue("complexObjects", complexObjects);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "complexObjects", complexObjects } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_ArrayOfComplexObjectsWithInvalidEnum_ThrowsMyApplicationException()
        {
            // Arrange
            var complexObjects = new ComplexObject[]
            {
                new ComplexObject { EnumProperty = TestEnum.Value1, StringProperty = "test" },
                new ComplexObject { EnumProperty = (TestEnum)999, StringProperty = "test" }
            };
            var actionContext = CreateActionContextWithParameterValue("complexObjects", complexObjects);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "complexObjects", complexObjects } }, null);

            // Act & Assert
            var exception = Assert.Throws<MyApplicationException>(() =>
                _validator.OnActionExecuting(actionExecutingContext));

            Assert.Equal(ErrorStatus.InvalidData, exception.ErrorStatus);
        }
        #endregion

        #region Empty Arrays and Collections Tests
        [Fact]
        public void OnActionExecuting_EmptyArray_DoesNotThrowException()
        {
            // Arrange
            var emptyArray = new TestEnum[] { };
            var actionContext = CreateActionContextWithParameterValue("emptyArray", emptyArray);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "emptyArray", emptyArray } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }
        #endregion

        #region String, DateTime, and File Type Handling
        [Fact]
        public void OnActionExecuting_DateTimeValue_DoesNotThrowException()
        {
            // Arrange
            var actionContext = CreateActionContextWithParameterValue("dateTimeValue", DateTimeOffset.Now);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "dateTimeValue", DateTimeOffset.Now } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_IFormFile_DoesNotThrowException()
        {
            // Arrange
            var mockFormFile = new Mock<IFormFile>();
            var actionContext = CreateActionContextWithParameterValue("iFormFile", mockFormFile.Object);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "iFormFile", mockFormFile.Object } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_StringValue_DoesNotThrowException()
        {
            // Arrange
            var actionContext = CreateActionContextWithParameterValue("stringValue", "testString");
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "stringValue", "testString" } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_FormFile_DoesNotThrowException()
        {
            // Arrange
            var mockFormFile = new Mock<IFormFile>();
            var actionContext = CreateActionContextWithParameterValue("formFile", mockFormFile.Object);
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "formFile", mockFormFile.Object } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_GuidValue_DoesNotThrowException()
        {
            // Arrange
            var actionContext = CreateActionContextWithParameterValue("guidValue", Guid.NewGuid());
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "guidValue", Guid.NewGuid() } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }

        [Fact]
        public void OnActionExecuting_UriValue_DoesNotThrowException()
        {
            // Arrange
            var actionContext = CreateActionContextWithParameterValue("uriValue", new Uri("https://example.com"));
            var actionExecutingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
                new Dictionary<string, object> { { "uriValue", new Uri("https://example.com") } }, null);

            // Act & Assert
            _validator.OnActionExecuting(actionExecutingContext);  // No exception should be thrown
        }
        #endregion

        #region Helper Methods
        private static ActionContext CreateActionContextWithParameterValue(string paramName, object paramValue)
        {
            var parameterDescriptor = new ControllerParameterDescriptor
            {
                Name = paramName,
                ParameterType = paramValue?.GetType() ?? typeof(object)
            };

            var actionDescriptor = new ControllerActionDescriptor
            {
                Parameters = new List<ParameterDescriptor> { parameterDescriptor }
            };

            var httpContext = new Mock<HttpContext>();  // Mock the HttpContext
            var routeData = new RouteData();

            return new ActionContext(httpContext.Object, routeData, actionDescriptor);
        }

        private static ActionContext CreateActionContext()
        {
            var parameterDescriptor = new ControllerParameterDescriptor
            {
                Name = "requiredParam",
                ParameterType = typeof(int)
            };

            var actionDescriptor = new ControllerActionDescriptor
            {
                Parameters = new List<ParameterDescriptor> { parameterDescriptor }
            };

            var httpContext = new Mock<HttpContext>();  // Mock the HttpContext
            var routeData = new RouteData();

            return new ActionContext(httpContext.Object, routeData, actionDescriptor);
        }
        #endregion
    }
}
