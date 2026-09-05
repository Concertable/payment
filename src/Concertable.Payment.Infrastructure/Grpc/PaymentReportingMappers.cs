using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Grpc;
using Google.Protobuf.WellKnownTypes;
using KernelCurrency = Concertable.Kernel.ValueObjects.Currency;
using KernelMoney = Concertable.Kernel.ValueObjects.Money;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class PaymentReportingMappers
{
    extension(IEnumerable<MonthlyPaymentTotal> totals)
    {
        public MonthlyPaymentSeriesResponse ToProtoResponse()
        {
            var response = new MonthlyPaymentSeriesResponse();
            response.Points.AddRange(totals.Select(total => new MonthlyPaymentPointResponse
            {
                Month = Timestamp.FromDateTime(DateTime.SpecifyKind(
                    total.Month.ToDateTime(TimeOnly.MinValue),
                    DateTimeKind.Utc)),
                Gross = KernelMoney.FromMinorUnits(total.GrossMinor, KernelCurrency.Gbp).ToProtoMoney(),
                Net = KernelMoney.FromMinorUnits(total.NetMinor, KernelCurrency.Gbp).ToProtoMoney(),
                Count = total.Count
            }));
            return response;
        }
    }

    extension(IEnumerable<SettlementSummary> settlements)
    {
        public SettlementReportResponse ToProtoResponse()
        {
            var response = new SettlementReportResponse();
            response.Items.AddRange(settlements.Select(settlement => new SettlementReportItemResponse
            {
                Id = settlement.Id,
                Reference = new Concertable.Payment.Grpc.PaymentOperationReference
                {
                    OperationType = settlement.Reference.OperationType,
                    ClientReference = settlement.Reference.ClientReference
                },
                PayerId = settlement.PayerId.ToString(),
                PayeeId = settlement.PayeeId.ToString(),
                Amount = KernelMoney.FromMinorUnits(settlement.AmountMinor, KernelCurrency.Gbp).ToProtoMoney(),
                At = Timestamp.FromDateTime(DateTime.SpecifyKind(settlement.At, DateTimeKind.Utc))
            }));
            return response;
        }
    }
}
