using Google.Protobuf.Reflection;

namespace Concertable.Payment.UnitTests.Compatibility;

internal static class ProtoSchemaSnapshot
{
    public static IReadOnlyList<string> Create(FileDescriptorProto file)
    {
        var rows = new List<string>
        {
            $"FILE|{file.Name}|package={file.Package}|syntax={file.Syntax}|csharp_namespace={file.Options?.CsharpNamespace}"
        };
        rows.AddRange(file.Dependency.Select(dependency => $"DEPENDENCY|{dependency}"));

        foreach (var @enum in file.EnumType)
            AddEnum(rows, file.Package, @enum);
        foreach (var message in file.MessageType)
            AddMessage(rows, file.Package, message);
        foreach (var service in file.Service)
        {
            var serviceName = Qualify(file.Package, service.Name);
            rows.Add($"SERVICE|{serviceName}");
            rows.AddRange(service.Method.Select(method =>
                $"METHOD|{serviceName}.{method.Name}|input={method.InputType}|output={method.OutputType}|client_streaming={method.ClientStreaming}|server_streaming={method.ServerStreaming}"));
        }

        return rows.Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddEnum(ICollection<string> rows, string parent, EnumDescriptorProto @enum)
    {
        var enumName = Qualify(parent, @enum.Name);
        rows.Add($"ENUM|{enumName}");
        foreach (var value in @enum.Value)
            rows.Add($"ENUM_VALUE|{enumName}.{value.Name}|{value.Number}");
        foreach (var range in @enum.ReservedRange)
            rows.Add($"ENUM_RESERVED_RANGE|{enumName}|{range.Start}:{range.End}");
        foreach (var name in @enum.ReservedName)
            rows.Add($"ENUM_RESERVED_NAME|{enumName}|{name}");
    }

    private static void AddMessage(ICollection<string> rows, string parent, DescriptorProto message)
    {
        var messageName = Qualify(parent, message.Name);
        rows.Add($"MESSAGE|{messageName}");
        foreach (var field in message.Field)
        {
            var oneof = field.HasOneofIndex ? field.OneofIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none";
            rows.Add($"FIELD|{messageName}.{field.Name}|number={field.Number}|type={field.Type}|type_name={field.TypeName}|label={field.Label}|oneof={oneof}|proto3_optional={field.Proto3Optional}");
        }
        foreach (var range in message.ReservedRange)
            rows.Add($"MESSAGE_RESERVED_RANGE|{messageName}|{range.Start}:{range.End}");
        foreach (var name in message.ReservedName)
            rows.Add($"MESSAGE_RESERVED_NAME|{messageName}|{name}");
        foreach (var @enum in message.EnumType)
            AddEnum(rows, messageName, @enum);
        foreach (var nested in message.NestedType)
            AddMessage(rows, messageName, nested);
    }

    private static string Qualify(string parent, string name) =>
        string.IsNullOrEmpty(parent) ? name : $"{parent}.{name}";
}
