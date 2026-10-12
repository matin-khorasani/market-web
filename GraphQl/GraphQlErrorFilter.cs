using HotChocolate;
using HotChocolate.Execution;
using Microsoft.EntityFrameworkCore;

public class GraphQlErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
        => error.Exception is DbUpdateConcurrencyException
            ? error.WithMessage("The data was changed by someone else. Please retry.")
                   .WithCode("CONFLICT")
            : error;
}