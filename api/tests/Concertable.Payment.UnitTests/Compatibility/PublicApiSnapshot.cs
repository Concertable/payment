using System.Reflection;

namespace Concertable.Payment.UnitTests.Compatibility;

internal static class PublicApiSnapshot
{
    public static IReadOnlyList<string> Create(Assembly assembly)
    {
        var signatures = new List<string>();

        foreach (var type in assembly.GetExportedTypes().OrderBy(FormatType, StringComparer.Ordinal))
        {
            signatures.Add(FormatTypeDeclaration(type));
            signatures.AddRange(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(constructor => $"CONSTRUCTOR|{FormatType(type)}({FormatParameters(constructor.GetParameters())})"));
            signatures.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(FormatField));
            signatures.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(property => property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true)
                .Select(FormatProperty));
            signatures.AddRange(type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(@event => @event.AddMethod?.IsPublic == true || @event.RemoveMethod?.IsPublic == true)
                .Select(FormatEvent));
            signatures.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !IsAccessor(method))
                .Select(FormatMethod));
        }

        return signatures.Order(StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> CreateMessageUrns(Assembly assembly) =>
        assembly.GetExportedTypes()
            .SelectMany(type => type.CustomAttributes
                .Where(attribute => attribute.AttributeType.FullName == "Concertable.Messaging.Contracts.MessageTypeAttribute")
                .Select(attribute => $"{FormatType(type)}|{attribute.ConstructorArguments.Single().Value}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string FormatTypeDeclaration(Type type)
    {
        var kind = type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct" : "class";
        var baseType = type.BaseType is null ? "none" : FormatType(type.BaseType);
        var interfaces = string.Join(",", type.GetInterfaces().Select(FormatType).Order(StringComparer.Ordinal));
        var constraints = type.IsGenericTypeDefinition ? FormatGenericConstraints(type.GetGenericArguments()) : string.Empty;
        return $"TYPE|{kind}|{FormatType(type)}|base={baseType}|interfaces={interfaces}|constraints={constraints}";
    }

    private static string FormatField(FieldInfo field)
    {
        var scope = field.IsStatic ? "static" : "instance";
        var mutability = field.IsLiteral ? $"const={FormatValue(field.GetRawConstantValue())}" : field.IsInitOnly ? "readonly" : "mutable";
        return $"FIELD|{FormatType(field.DeclaringType!)}.{field.Name}|{scope}|{mutability}|{FormatType(field.FieldType)}";
    }

    private static string FormatProperty(PropertyInfo property)
    {
        var accessors = new List<string>();
        if (property.GetMethod?.IsPublic == true)
            accessors.Add("get");
        if (property.SetMethod?.IsPublic == true)
            accessors.Add(IsInitOnly(property.SetMethod) ? "init" : "set");

        var index = property.GetIndexParameters();
        var indexSignature = index.Length == 0 ? string.Empty : $"[{FormatParameters(index)}]";
        return $"PROPERTY|{FormatType(property.DeclaringType!)}.{property.Name}{indexSignature}|{FormatType(property.PropertyType)}|{string.Join(",", accessors)}";
    }

    private static string FormatEvent(EventInfo @event) =>
        $"EVENT|{FormatType(@event.DeclaringType!)}.{@event.Name}|{FormatType(@event.EventHandlerType!)}";

    private static string FormatMethod(MethodInfo method)
    {
        var scope = method.IsStatic ? "static" : "instance";
        var genericArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : [];
        var genericSuffix = genericArguments.Length == 0 ? string.Empty : $"<{string.Join(",", genericArguments.Select(argument => argument.Name))}>";
        var constraints = FormatGenericConstraints(genericArguments);
        return $"METHOD|{FormatType(method.DeclaringType!)}.{method.Name}{genericSuffix}({FormatParameters(method.GetParameters())})|{scope}|returns={FormatType(method.ReturnType)}|constraints={constraints}";
    }

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters) =>
        string.Join(",", parameters.Select(FormatParameter));

    private static string FormatParameter(ParameterInfo parameter)
    {
        var direction = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef && parameter.IsIn ? "in " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
        var optional = parameter.HasDefaultValue ? $"={FormatValue(parameter.DefaultValue)}" : string.Empty;
        return $"{direction}{FormatType(parameter.ParameterType)} {parameter.Name}{optional}";
    }

    private static string FormatGenericConstraints(IEnumerable<Type> arguments) =>
        string.Join(";", arguments.Select(argument =>
        {
            var attributes = argument.GenericParameterAttributes;
            var constraints = argument.GetGenericParameterConstraints().Select(FormatType).Order(StringComparer.Ordinal).ToList();
            if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                constraints.Insert(0, "class");
            if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                constraints.Insert(0, "struct");
            if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                constraints.Add("new()");
            return $"{argument.Name}:{string.Join(",", constraints)}";
        }));

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
            return FormatType(type.GetElementType()!);
        if (type.IsArray)
            return $"{FormatType(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
        if (type.IsPointer)
            return $"{FormatType(type.GetElementType()!)}*";
        if (type.IsGenericParameter)
            return type.Name;
        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var definitionName = type.GetGenericTypeDefinition().FullName!;
        var tick = definitionName.IndexOf('`', StringComparison.Ordinal);
        var name = tick < 0 ? definitionName : definitionName[..tick];
        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(FormatType))}>";
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        char character => $"'{character}'",
        bool boolean => boolean ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null"
    };

    private static bool IsAccessor(MethodInfo method) =>
        method.IsSpecialName && (method.Name.StartsWith("get_", StringComparison.Ordinal)
            || method.Name.StartsWith("set_", StringComparison.Ordinal)
            || method.Name.StartsWith("add_", StringComparison.Ordinal)
            || method.Name.StartsWith("remove_", StringComparison.Ordinal));

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
