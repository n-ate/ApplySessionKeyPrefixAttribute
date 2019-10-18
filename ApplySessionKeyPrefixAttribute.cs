using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.SessionState;

namespace LeVel.Presentation.Common
{
    /// <summary>
    /// Applies a case-insensitive prefix to all Session variables.
    /// </summary>
    public class ApplySessionKeyPrefixAttribute : ActionFilterAttribute
    {
        private string[] ignoreKeys;
        private string prefix;

        public ApplySessionKeyPrefixAttribute(string prefix, params string[] ignoreKeys)
        {
            this.prefix = prefix.ToUpper();
            this.ignoreKeys = ignoreKeys ?? new string[0];
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            filterContext.Controller.ControllerContext = new CustomControllerContext(filterContext.Controller.ControllerContext, prefix, ignoreKeys);//wraps the current ControllerContext
            base.OnActionExecuting(filterContext);
        }
    }

    internal class CustomControllerContext : ControllerContext
    {
        private ControllerContext context;
        private string[] ignoreKeys;
        private string prefix;

        public CustomControllerContext(ControllerContext context, string prefix, string[] ignoreKeys) : base(context)
        {
            this.context = context;
            this.prefix = prefix.ToUpper();
            this.ignoreKeys = ignoreKeys ?? new string[0];
        }

        public override HttpContextBase HttpContext
        {
            get => new CustomHttpContext(context.HttpContext as HttpContextWrapper, prefix, ignoreKeys);//wraps the current HttpContext
            set => context.HttpContext = value;
        }
    }

    internal class CustomHttpContext : HttpContextWrapper
    {
        private HttpContextBase context;
        private string[] ignoreKeys;
        private string prefix;

        public CustomHttpContext(HttpContextWrapper context, string prefix, string[] ignoreKeys) : base(context.ApplicationInstance.Context)
        {
            this.context = context;
            this.prefix = prefix.ToUpper();
            var list = new List<string>(ignoreKeys ?? new string[0]);
            list.Add("__ControllerTempData");//this is the MS key used to store TempData
            this.ignoreKeys = list.ToArray();
        }

        public override HttpSessionStateBase Session => new PrefixedSession(context.Session as HttpSessionStateWrapper, prefix, ignoreKeys);//wraps the current session object
    }

    internal class PrefixedSession : HttpSessionStateWrapper
    {
        private string[] ignoreKeys;
        private string prefix;
        private HttpSessionStateBase session;

        public PrefixedSession(HttpSessionStateWrapper session, string prefix, string[] ignoreKeys) : base(GetFieldValue<HttpSessionState>(session, "_session"))
        {
            this.session = session;
            this.prefix = $"[{prefix.ToUpper()}]";
            this.ignoreKeys = ignoreKeys ?? new string[0];
        }

        public override object this[string name] //appends prefix
        {
            get => (ignoreKeys.Contains(name) || !session.Keys.Cast<string>().Contains(prefix + name)) ? session[name] : session[prefix + name];
            set
            {
                if (ignoreKeys.Contains(name)) session[name] = value;
                else session[prefix + name] = value;
            }
        }

        public override void Add(string name, object value) //appends prefix
        {
            if (ignoreKeys.Contains(name)) session.Add(name, value);
            else session.Add(prefix + name, value);
        }

        private static TReturn GetFieldValue<TReturn>(object source, string fieldName)
        {
            var field = source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return (TReturn)field.GetValue(source);
        }
    }
}