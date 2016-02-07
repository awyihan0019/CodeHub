using System;
using UIKit;
using Foundation;
using CodeHub.Core.ViewModels;
using WebKit;
using MvvmCross.iOS.Views;

namespace CodeHub.iOS.Views
{
    public class WebView : MvxViewController
    {
        protected UIBarButtonItem BackButton;
        protected UIBarButtonItem RefreshButton;
        protected UIBarButtonItem ForwardButton;

        public WKWebView Web { get; private set; }
        private readonly bool _navigationToolbar;
        private readonly  bool _showPageAsTitle;

		bool _appeared;
		public override void ViewDidAppear (bool animated)
		{
			base.ViewDidAppear (animated);
			if (!_appeared) {
				_appeared = true;
			}
		}

        protected virtual void GoBack()
        {
            Web.GoBack();
        }

        protected virtual void Refresh()
        {
            Web.Reload();
        }

        protected virtual void GoForward()
        {
            Web.GoForward();
        }
         
		public WebView()
			: this(true, true)
        {
        }

		public WebView(bool navigationToolbar, bool showPageAsTitle = false)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem() { Title = "" };

            _navigationToolbar = navigationToolbar;
            _showPageAsTitle = showPageAsTitle;

            if (_navigationToolbar)
            {
                BackButton = new UIBarButtonItem(Theme.CurrentTheme.WebBackButton, UIBarButtonItemStyle.Plain, (s, e) => GoBack()) { Enabled = false };
                ForwardButton = new UIBarButtonItem(Theme.CurrentTheme.WebFowardButton, UIBarButtonItemStyle.Plain, (s, e) => GoForward()) { Enabled = false };
                RefreshButton = new UIBarButtonItem(UIBarButtonSystemItem.Refresh, (s, e) => Refresh()) { Enabled = false };

                BackButton.TintColor = Theme.CurrentTheme.WebButtonTint;
                ForwardButton.TintColor = Theme.CurrentTheme.WebButtonTint;
                RefreshButton.TintColor = Theme.CurrentTheme.WebButtonTint;
            }

			EdgesForExtendedLayout = UIRectEdge.None;
        }

        private class NavigationDelegate : WKNavigationDelegate
        {
            private readonly WeakReference<WebView> _webView;

            public NavigationDelegate(WebView webView)
            {
                _webView = new WeakReference<WebView>(webView);
            }

            public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
            {
                _webView.Get()?.OnLoadFinished(null, EventArgs.Empty);
            }

            public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
            {
                _webView.Get()?.OnLoadStarted(null, EventArgs.Empty);
            }

            public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
            {
                _webView.Get()?.OnLoadError(error);
            }

            public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
            {
                var ret = _webView.Get()?.ShouldStartLoad(webView, navigationAction) ?? true;
                decisionHandler(ret ? WKNavigationActionPolicy.Allow : WKNavigationActionPolicy.Cancel);
            }
        }

        protected virtual bool ShouldStartLoad (WKWebView webView, WKNavigationAction navigationAction)
        {
            return true;
        }

        protected virtual void OnLoadError (NSError error)
        {
            MonoTouch.Utilities.PopNetworkActive();

            if (BackButton != null)
            {
                BackButton.Enabled = Web.CanGoBack;
                ForwardButton.Enabled = Web.CanGoForward;
                RefreshButton.Enabled = true;
            }
        }

        protected virtual void OnLoadStarted (object sender, EventArgs e)
        {
            MonoTouch.Utilities.PushNetworkActive();

            if (RefreshButton != null)
                RefreshButton.Enabled = false;
        }

        protected virtual void OnLoadFinished(object sender, EventArgs e)
        {
            MonoTouch.Utilities.PopNetworkActive();

            if (BackButton != null)
            {
                BackButton.Enabled = Web.CanGoBack;
                ForwardButton.Enabled = Web.CanGoForward;
                RefreshButton.Enabled = true;
            }

            if (_showPageAsTitle)
            {
                Web.EvaluateJavaScript("document.title", (o, _) => {
                    Title = o as NSString;
                });
            }
        }
        
        public override void ViewWillDisappear(bool animated)
        {
            base.ViewWillDisappear(animated);
            if (ToolbarItems != null)
                NavigationController.SetToolbarHidden(true, animated);
        }
        
        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            Web = new WKWebView(View.Bounds, new WKWebViewConfiguration());
            Web.NavigationDelegate = new NavigationDelegate(this);
            Add(Web);

			var loadableViewModel = ViewModel as LoadableViewModel;
			if (loadableViewModel != null)
			{
				loadableViewModel.Bind(x => x.IsLoading, x =>
				{
					if (x) MonoTouch.Utilities.PushNetworkActive();
					else MonoTouch.Utilities.PopNetworkActive();
				});
			}
        }

        public override void ViewWillLayoutSubviews()
        {
            base.ViewWillLayoutSubviews();
            Web.Frame = View.Bounds;
        }

		protected static string JavaScriptStringEncode(string data)
		{
			return System.Web.HttpUtility.JavaScriptStringEncode(data);
		}

		protected static string UrlDecode(string data)
		{
			return System.Web.HttpUtility.UrlDecode(data);
		}

		protected string LoadFile(string path)
        {
			if (path == null)
				return string.Empty;

            var uri = Uri.EscapeUriString("file://" + path) + "#" + Environment.TickCount;
            InvokeOnMainThread(() => Web.LoadRequest(new Foundation.NSUrlRequest(new Foundation.NSUrl(uri))));
            return uri;
        }

		protected void LoadContent(string content)
		{
            Web.LoadHtmlString(content, NSBundle.MainBundle.BundleUrl);
		}
        
        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            var bounds = View.Bounds;
            if (_navigationToolbar)
                bounds.Height -= NavigationController.Toolbar.Frame.Height;
            Web.Frame = bounds;

            if (_navigationToolbar)
            {
                ToolbarItems = new []
                { 
                    BackButton,
                    new UIBarButtonItem(UIBarButtonSystemItem.FixedSpace) { Width = 40f },
                    ForwardButton,
                    new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                    RefreshButton
                };

                BackButton.Enabled = Web.CanGoBack;
                ForwardButton.Enabled = Web.CanGoForward;
                RefreshButton.Enabled = !Web.IsLoading;
            }   

            if (_showPageAsTitle)
            {
                Web.EvaluateJavaScript("document.title", (o, _) => {
                    Title = o as NSString;
                });
            }

            if (ToolbarItems != null)
                NavigationController.SetToolbarHidden(false, animated);
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            if (_navigationToolbar)
                ToolbarItems = null;
        }

        public override void DidRotate(UIInterfaceOrientation fromInterfaceOrientation)
        {
            base.DidRotate(fromInterfaceOrientation);
            Web.Frame = View.Bounds;
        }
    }
}
