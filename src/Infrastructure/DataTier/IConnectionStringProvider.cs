namespace EquiLink.Infrastructure.DataTier;

public interface IConnectionStringProvider
{
    string GetWriteConnectionString();
    string GetReadConnectionString();
}
