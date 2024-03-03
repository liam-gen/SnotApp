using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
//using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Collections;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Diagnostics;
using System.Xml;
using Newtonsoft.Json;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Security.Policy;

namespace SnotApp
{
    public partial class Form1 : Form
    {
        public static string pluginsPath = @Directory.GetCurrentDirectory() + "\\plugins";
        public static string themesPath = @Directory.GetCurrentDirectory() + "\\themes";
        public static JToken themesList;
        public static JToken pluginsList;
        public WebView2 webview;
        public Form1()
        {
            InitializeComponent();

            ((Action)(async () =>
            {
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, "../userdata");
                webview = new WebView2();

                Form f = new Form
                {
                    Size = new Size(500, 500)
                };

                var result = webview.EnsureCoreWebView2Async(env).GetAwaiter();
                result.OnCompleted(() =>
                {
                    try
                    {
                        result.GetResult();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });

                webview.AllowExternalDrop = true;
                webview.DefaultBackgroundColor = Color.White;
                webview.Location = new Point(-1, -3);
                webview.Size = new Size(46, 59);
                webview.Source = new Uri("https://snot.fr/fr/login", UriKind.Absolute);
                webview.TabIndex = 0;
                webview.ZoomFactor = 1D;

                this.Controls.Add(webview);

                webview.NavigationStarting += CoreWebView2_WebResourceRequested;

                

                this.Resize += new System.EventHandler(this.Form_Resize);
                InitializeAsync();

                try
                {
                    themesList = JsonConvert.DeserializeObject<JToken>(httpGet("https://content.snot.fr/better-snot/themes/themes.json"));
                }
                catch (Exception e)
                {
                    await webview.CoreWebView2.ExecuteScriptAsync($"console.error(`(BS) - {e.Message}`)");
                }

                try
                {
                    pluginsList = JsonConvert.DeserializeObject<JToken>(httpGet("https://content.snot.fr/better-snot/plugins/plugins.json"));
                }
                catch (Exception e)
                {
                    await webview.CoreWebView2.ExecuteScriptAsync($"console.error(`(BS) - {e.Message}`)");
                }
            })).Invoke();




        }

        async private void CoreWebView2_WebResourceRequested(object sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (!args.Uri.StartsWith("https://snot.fr"))
            {
                args.Cancel = true;
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            }
        }

        async void InitializeAsync()
        {

            Directory.CreateDirectory(pluginsPath);
            Directory.CreateDirectory(themesPath);
            await webview.EnsureCoreWebView2Async(null);

            webview.CoreWebView2.NavigationCompleted += async (sender, e) =>
            {
                LoadPlugins();
                LoadThemes();
            };


            webview.CoreWebView2.ContextMenuRequested += delegate (object sender,
                                    CoreWebView2ContextMenuRequestedEventArgs args)
            {
                IList<CoreWebView2ContextMenuItem> menuList = args.MenuItems;
                // add new item to end of collection
                CoreWebView2ContextMenuItem newItem =
                                    webview.CoreWebView2.Environment.CreateContextMenuItem(
                    "Dossier des plugins", null, CoreWebView2ContextMenuItemKind.Command);
                newItem.CustomItemSelected += delegate (object send, Object ex)
                {
                    System.Threading.SynchronizationContext.Current.Post((_) =>
                    {
                        Process.Start("explorer.exe", pluginsPath);
                        //MessageBox.Show(pageUri, "Page Uri", MessageBoxButtons.OK);
                    }, null);
                };
                menuList.Insert(menuList.Count, newItem);

                CoreWebView2ContextMenuItem newItem2 =
                                    webview.CoreWebView2.Environment.CreateContextMenuItem(
                    "Dossier des thèmes", null, CoreWebView2ContextMenuItemKind.Command);
                newItem2.CustomItemSelected += delegate (object send, Object ex)
                {
                    System.Threading.SynchronizationContext.Current.Post((_) =>
                    {
                        Process.Start("explorer.exe", themesPath);
                        //MessageBox.Show(pageUri, "Page Uri", MessageBoxButtons.OK);
                    }, null);
                };
                menuList.Insert(menuList.Count, newItem2);

                CoreWebView2ContextMenuItem newItem3 =
                                    webview.CoreWebView2.Environment.CreateContextMenuItem(
                    "Gestionnaire des taches", null, CoreWebView2ContextMenuItemKind.Command);
                newItem3.CustomItemSelected += delegate (object send, Object ex)
                {
                    System.Threading.SynchronizationContext.Current.Post((_) =>
                    {
                        webview.CoreWebView2.OpenTaskManagerWindow();
                        //MessageBox.Show(pageUri, "Page Uri", MessageBoxButtons.OK);
                    }, null);
                };

                menuList.Insert(menuList.Count, newItem3);
            };

        }

        private void Form_Resize(object sender, EventArgs e)
        {
            webview.Size = this.ClientSize - new System.Drawing.Size(webview.Location);
        }

        async public void LoadPlugins()
        {
            foreach (string path in Directory.GetFiles(pluginsPath))
            {
                var js = File.ReadAllText(path);
                if (File.Exists(path))
                {
                    try
                    {
                        Regex regex = new Regex(@"\/\*([\s\S]*?)\*\/");
                        MatchCollection matches = regex.Matches(js);

                        JObject metadataJson = new JObject();

                        foreach (Match match in matches)
                        {
                            string metadata = match.Groups[1].Value.Trim();
                            string[] lines = metadata.Split('\n');

                            foreach (string line in lines)
                            {
                                if (line.Contains(":"))
                                {
                                    string[] parts = line.Split(':');
                                    string key = parts[0].Trim();
                                    string value = parts[1].Trim();

                                    metadataJson[key] = value;
                                }
                            }
                        }

                        if (metadataJson["Version"] != null && metadataJson["ID"] != null)
                        {
                            var ID = metadataJson["ID"].ToString();
                            var localVersion = new Version(metadataJson["Version"].ToString());
                            var latestVersion = new Version(pluginsList[ID]["version"].ToString());

                            if (latestVersion != null && localVersion.CompareTo(latestVersion) == -1)
                            {
                                // Besoin d'une mise à jour
                                var newJS = httpGet(pluginsList[ID]["url"].ToString());

                                if (newJS != null)
                                {
                                    using (StreamWriter outputFile = new StreamWriter(path))
                                    {
                                        await outputFile.WriteAsync(newJS);

                                        if (metadataJson["Name"] != null)
                                        {
                                            MessageBox.Show("Le plugin " + metadataJson["Name"].ToString() + " à été mis à jour !");
                                        }
                                    }

                                }
                            }
                        }

                        await webview.CoreWebView2.ExecuteScriptAsync(js);

                        if (metadataJson["Name"] != null)
                        {
                            var ID = metadataJson["ID"] != null ? "(" + metadataJson["ID"] + ") " : "";
                            var version = metadataJson["Version"] != null ? "v" + metadataJson["Version"] + " " : "";
                            await webview.CoreWebView2.ExecuteScriptAsync($"console.log(`(BS) - Plugin \"{metadataJson["Name"]}\" {ID}{version}loaded !`)");
                        }
                    }
                    catch (Exception e)
                    {
                        await webview.CoreWebView2.ExecuteScriptAsync(js);
                    }

                }
            }
        }

        public static string httpGet(string url)
        {
            WebRequest request = HttpWebRequest.Create(url);

            request.Method = "GET";
            request.Headers.Add("User-Agent", "Mozilla/5.0");

            WebResponse response = request.GetResponse();

            StreamReader reader = new StreamReader(response.GetResponseStream());

            return reader.ReadToEnd();
        }

        async public void LoadThemes()
        {

            try
            {
                if (themesList == null)
                {
                    try
                    {
                        themesList = JsonConvert.DeserializeObject<JToken>(httpGet("https://content.snot.fr/better-snot/themes/themes.json"));
                    }
                    catch (Exception a)
                    {
                        throw new InvalidOperationException("La liste des thèmes n'a pas pu être chargée");
                    }
                }

                foreach (string path in Directory.GetFiles(themesPath))
                {
                    if (File.Exists(path))
                    {
                        string css = File.ReadAllText(path);

                        Regex regex = new Regex(@"\/\*([\s\S]*?)\*\/");
                        MatchCollection matches = regex.Matches(css);

                        JObject metadataJson = new JObject();

                        foreach (Match match in matches)
                        {
                            string metadata = match.Groups[1].Value.Trim();
                            string[] lines = metadata.Split('\n');

                            foreach (string line in lines)
                            {
                                if (line.Contains(":"))
                                {
                                    string[] parts = line.Split(':');
                                    string key = parts[0].Trim();
                                    string value = parts[1].Trim();

                                    metadataJson[key] = value;
                                }
                            }
                        }

                        if (metadataJson["Version"] != null && metadataJson["ID"] != null)
                        {
                            var ID = metadataJson["ID"].ToString();
                            var localVersion = new Version(metadataJson["Version"].ToString());
                            var latestVersion = new Version(themesList[ID]["version"].ToString());

                            if (latestVersion != null && localVersion.CompareTo(latestVersion) == -1)
                            {
                                // Besoin d'une mise à jour
                                var newCSS = httpGet(themesList[ID]["url"].ToString());

                                if (newCSS != null)
                                {
                                    using (StreamWriter outputFile = new StreamWriter(path))
                                    {
                                        await outputFile.WriteAsync(newCSS);

                                        if (metadataJson["Name"] != null)
                                        {
                                            MessageBox.Show("Le thème " + metadataJson["Name"].ToString() + " à été mis à jour !");
                                        }
                                    }

                                }
                            }
                        }

                        string code = "var link = document.createElement('style');link.innerHTML = `" + css + "`;document.head.appendChild(link);";

                        await webview.CoreWebView2.ExecuteScriptAsync(code);

                        var ID_ = metadataJson["ID"] != null ? "(" + metadataJson["ID"] + ") " : "";
                        var version = metadataJson["Version"] != null ? "v" + metadataJson["Version"] + " " : "";

                        if (metadataJson["Name"] != null)
                            await webview.CoreWebView2.ExecuteScriptAsync($"console.log(`(BS) - Theme \"{metadataJson["Name"]}\" {ID_}{version}loaded !`)");
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                foreach (string path in Directory.GetFiles(themesPath))
                {
                    if (File.Exists(path))
                    {
                        string css = File.ReadAllText(path);
                        string code = "var link = document.createElement('style');link.innerHTML = `" + css + "`;document.head.appendChild(link);";

                        await webview.CoreWebView2.ExecuteScriptAsync(code);
                    }
                }
            }

        }
    }
}


/* Changelogs
 * Ajout du gestionnaire de taches
 * 
 * 
*/