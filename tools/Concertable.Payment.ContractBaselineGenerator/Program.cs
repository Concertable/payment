using Concertable.Payment.Client;
using Concertable.Payment.Contracts;
using Concertable.Payment.Grpc;
using Concertable.Payment.UnitTests.Compatibility;
using Google.Protobuf;
using Google.Protobuf.Reflection;

if (args.Length != 1)
    throw new ArgumentException("Pass the output directory as the only argument.");

var outputDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputDirectory);

WriteLines("Concertable.Payment.Contracts.public-api.txt", PublicApiSnapshot.Create(typeof(PaymentSession).Assembly));
WriteLines("Concertable.Payment.Contracts.message-urns.txt", PublicApiSnapshot.CreateMessageUrns(typeof(PaymentSession).Assembly));
WriteLines(
    "Concertable.Payment.Client.public-api.txt",
    PublicApiSnapshot.Create(typeof(Concertable.Payment.Client.PaymentOperationSnapshot).Assembly));

var descriptorSet = new FileDescriptorSet();
descriptorSet.File.Add(PaymentReflection.Descriptor.ToProto());
File.WriteAllText(Path.Combine(outputDirectory, "payment.protoset.base64"), Convert.ToBase64String(descriptorSet.ToByteArray()) + Environment.NewLine);

void WriteLines(string fileName, IEnumerable<string> lines) =>
    File.WriteAllLines(Path.Combine(outputDirectory, fileName), lines);
