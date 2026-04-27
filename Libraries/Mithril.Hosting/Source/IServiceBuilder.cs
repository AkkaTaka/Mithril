namespace Mithril.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public interface IServiceBuilder
{
  public void Build(HostBuilderContext context, IServiceCollection service);
}
