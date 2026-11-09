using Microsoft.Extensions.Options;
using HvacErp.Application.Erp;

namespace HvacErp.Infrastructure.Erp;

/// <summary>
/// Picks the connector named by <c>Erp:Provider</c> among the registered ones. Adding an ERP means
/// registering another <see cref="IErpConnector"/>; the choice is configuration.
/// </summary>
public sealed class ErpConnectorResolver(IEnumerable<IErpConnector> connectors, IOptionsMonitor<ErpOptions> options)
    : IErpConnectorResolver
{
    /// <inheritdoc />
    public IErpConnector? Current => ByName(options.CurrentValue.Provider);

    /// <inheritdoc />
    public IErpConnector? ByName(string name) =>
        string.Equals(name, "None", StringComparison.OrdinalIgnoreCase)
            ? null
            : connectors.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
