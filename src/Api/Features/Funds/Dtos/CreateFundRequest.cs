using System.ComponentModel.DataAnnotations;

namespace EquiLink.Api.Features.Funds.Dtos;

public record CreateFundRequest(
    [Required, MaxLength(256)] string Name,
    [Required, MaxLength(256)] string ManagerName,
    [Required, MaxLength(256)] string RiskLimitTemplateName,
    [Required, Range(0.00000001, double.MaxValue)] decimal MaxOrderSize,
    [Required, Range(0.00000001, double.MaxValue)] decimal DailyLossLimit,
    [Required, Range(0.00000001, double.MaxValue)] decimal ConcentrationLimit
);
