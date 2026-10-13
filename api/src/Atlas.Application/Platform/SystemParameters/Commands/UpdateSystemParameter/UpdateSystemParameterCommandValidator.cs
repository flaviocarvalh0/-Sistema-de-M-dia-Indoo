namespace Atlas.Application.Platform.SystemParameters.Commands.UpdateSystemParameter;

public class UpdateSystemParameterCommandValidator : AbstractValidator<UpdateSystemParameterCommand>
{
    public UpdateSystemParameterCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0).WithMessage("O ID do parâmetro é obrigatório.");

        RuleFor(v => v.Key)
            .NotEmpty().WithMessage("A chave do parâmetro é obrigatória.")
            .MaximumLength(100).WithMessage("A chave deve ter no máximo 100 caracteres.");

        RuleFor(v => v.Group)
            .NotEmpty().WithMessage("O grupo do parâmetro é obrigatório.");

        RuleFor(v => v.DataType)
            .NotEmpty().WithMessage("O tipo de dado é obrigatório.")
            .Must(type => new[] { "STRING", "NUMBER", "BOOLEAN", "JSON" }.Contains(type.ToUpperInvariant()))
            .WithMessage("Tipo de dado inválido. Use String, Number, Boolean ou Json.");

        RuleFor(v => v.Value)
            .NotEmpty().WithMessage("O valor do parâmetro não pode ser vazio.");

        RuleFor(v => v.Version)
            .GreaterThan(0u).WithMessage("A versão do parâmetro é obrigatória para o controle de concorrência.");

        // Validação do valor em relação ao DataType informado
        RuleFor(x => x).Custom((command, context) =>
        {
            if (string.IsNullOrWhiteSpace(command.DataType) || string.IsNullOrWhiteSpace(command.Value)) return;

            switch (command.DataType.ToUpperInvariant())
            {
                case "BOOLEAN":
                    if (!bool.TryParse(command.Value, out _))
                        context.AddFailure("Value", "O valor informado não corresponde a um booleano válido ('true' ou 'false').");
                    break;

                case "NUMBER":
                    if (!decimal.TryParse(command.Value, out _))
                        context.AddFailure("Value", "O valor informado não corresponde a um número válido.");
                    break;

                case "JSON":
                    if (!IsValidJson(command.Value))
                        context.AddFailure("Value", "O valor informado não corresponde a uma estrutura JSON válida.");
                    break;
            }
        });
    }

    private static bool IsValidJson(string stringValue)
    {
        if (string.IsNullOrWhiteSpace(stringValue)) return false;
        stringValue = stringValue.Trim();
        if ((stringValue.StartsWith("{") && stringValue.EndsWith("}")) ||
            (stringValue.StartsWith("[") && stringValue.EndsWith("]")))
        {
            try
            {
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(stringValue);
                return true;
            }
            catch { return false; }
        }
        return false;
    }
}
