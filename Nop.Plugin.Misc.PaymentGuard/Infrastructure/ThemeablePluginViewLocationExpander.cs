using Microsoft.AspNetCore.Mvc.Razor;
using System.Collections.Generic;
using System.Linq;

namespace Nop.Plugin.Misc.PaymentGuard.Infrastructure
{
    public class ThemeablePluginViewLocationExpander : IViewLocationExpander
    {
        private const string THEME_KEY = "nop.themename";

        public ThemeablePluginViewLocationExpander()
        {
        }

        protected virtual IList<string> GetPluginAdminViewLocations()
        {
            List<string> list = new List<string>
            {
                string.Concat("~/Plugins/", PaymentGuardDefaults.SystemName, "/Areas/Admin/Views/{1}/{0}.cshtml"),
                string.Concat("~/Plugins/", PaymentGuardDefaults.SystemName, "/Areas/Admin/Views/{0}.cshtml")
            };
            return list;
        }

        protected virtual IList<string> GetPluginViewLocations()
        {
            List<string> list = new List<string>
            {
                string.Concat("~/Plugins/", PaymentGuardDefaults.SystemName, "/Themes/{2}/Views/{1}/{0}.cshtml"),
                string.Concat("~/Plugins/", PaymentGuardDefaults.SystemName, "/Themes/{2}/Views/{0}.cshtml"),
                string.Concat("~/Plugins/", PaymentGuardDefaults.SystemName, "/Views/{1}/{0}.cshtml"),
                string.Concat("~/Plugins/", PaymentGuardDefaults.SystemName, "/Views/{0}.cshtml")
            };
            return list;
        }

        private List<string> GetThemeViewLocationFormats(IList<string> viewLocations, string theme)
        {
            return viewLocations.Select(x => x.Replace("{2}", theme)).ToList();
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            string str = null;
            if (context.AreaName == null)
            {
                if (context.Values.TryGetValue(THEME_KEY, out str))
                {
                    viewLocations = Enumerable.Concat<string>(this.GetThemeViewLocationFormats(this.GetPluginViewLocations(), str), viewLocations);
                }
            }
            else if (context.AreaName == "Admin")
            {
                viewLocations = Enumerable.Concat<string>(viewLocations, GetPluginAdminViewLocations());
            }

            return viewLocations;
        }

        public void PopulateValues(ViewLocationExpanderContext context)
        {
            //Nothing to do
        }
    }
}
