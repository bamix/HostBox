using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace HostBox
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services) => services.AddMvc();

        public void Configure(IApplicationBuilder app)
        {
            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}