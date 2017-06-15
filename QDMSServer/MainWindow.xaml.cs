﻿// -----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="">
// Copyright 2017 Alexander Soffronow Pagonidis
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Common.Logging.NLog;
using EntityData;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using NLog;
using NLog.Targets;
using QDMS;
using QDMSServer.Properties;
using Nancy.Hosting.Self;
using Nancy.Authentication.Stateless;
using QDMS.Server;
using QDMS.Server.Nancy;
using QDMS.Server.Repositories;
using QDMS.Server.DataSources;
using QDMSServer.ViewModels;

namespace QDMSServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private readonly QDMSClient.QDMSClient _client;
        private readonly Logger _clientLogger = LogManager.GetLogger("client");

        private ProgressBar _progressBar;
        
        private MainViewModel ViewModel { get; }
        public MainWindow()
        {
            Common.Logging.LogManager.Adapter = new NLogLoggerFactoryAdapter(new Common.Logging.Configuration.NameValueCollection());

            //make sure we can connect to the database
            CheckDBConnection();

            //set the log directory
            SetLogDirectory();

            //set the connection string
            DBUtils.SetConnectionString();

            //set EF configuration, necessary for MySql to work
            DBUtils.SetDbConfiguration();

            InitializeComponent();

            //load datagrid layout
            string layoutFile = AppDomain.CurrentDomain.BaseDirectory + "GridLayout.xml";
            if (File.Exists(layoutFile))
            {
                try
                {
                    InstrumentsGrid.DeserializeLayout(File.ReadAllText(layoutFile));
                }
                catch 
                {
                }
            }

            //Log unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += AppDomain_CurrentDomain_UnhandledException;

            //build the instruments grid context menu
            //we want a button for each BarSize enum value in the UpdateFreqSubMenu menu
            foreach (int value in Enum.GetValues(typeof(BarSize)))
            {
                var button = new MenuItem
                {
                    Header = Regex.Replace(((BarSize)value).ToString(), "([A-Z])", " $1").Trim(),
                    Tag = (BarSize)value
                };
                button.Click += UpdateHistoricalDataBtn_ItemClick;
                ((MenuItem)Resources["UpdateFreqSubMenu"]).Items.Add(button);
            }

            //create metadata db if it doesn't exist
            var entityContext = new MyDBContext();
            entityContext.Database.Initialize(false);

            //seed the datasources no matter what, because these are added frequently
            Seed.SeedDatasources(entityContext);

            //check for any exchanges, seed the db with initial values if nothing is found
            if (!entityContext.Exchanges.Any())
            {
                Seed.DoSeed();
            }

            //create data db if it doesn't exist
            var dataContext = new DataDBContext();
            dataContext.Database.Initialize(false);
            dataContext.Dispose();

            //create quartz db if it doesn't exist
            QuartzUtils.InitializeDatabase(Settings.Default.databaseType);

            //build the tags menu
            var allTags = entityContext.Tags.ToList();
            BuildTagContextMenu(allTags);

            //build session templates menu
            BuildSetSessionTemplateMenu();

<<<<<<< HEAD
            Instruments = new ObservableCollection<Instrument>();

            var instrumentRepo = new InstrumentRepository(entityContext);
            var instrumentList = instrumentRepo.FindInstruments().Result;

            foreach (Instrument i in instrumentList)
            {
                Instruments.Add(i);
            }

            //create brokers
            var cfRealtimeBroker = new ContinuousFuturesBroker(new QDMSClient.QDMSClient(
                    "RTDBCFClient",
                    "127.0.0.1",
                    Properties.Settings.Default.rtDBReqPort,
                    Properties.Settings.Default.rtDBPubPort,
                    Properties.Settings.Default.hDBPort,
                    Properties.Settings.Default.httpPort,
                    Properties.Settings.Default.apiKey,
                    useSsl: Properties.Settings.Default.useSsl), 
                    connectImmediately: false);
            var cfHistoricalBroker = new ContinuousFuturesBroker(new QDMSClient.QDMSClient(
                    "HDBCFClient",
                    "127.0.0.1",
                    Properties.Settings.Default.rtDBReqPort,
                    Properties.Settings.Default.rtDBPubPort,
                    Properties.Settings.Default.hDBPort,
                    Properties.Settings.Default.httpPort,
                    Properties.Settings.Default.apiKey,
                    useSsl: Properties.Settings.Default.useSsl), 
                    connectImmediately: false);
            var localStorage = DataStorageFactory.Get();

            var realTimeDataSourceList = new List<IRealTimeDataSource>();
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ibClientHost) &&
                Properties.Settings.Default.ibClientPort != default(int) &&
                Properties.Settings.Default.rtdClientIBID != default(int))
            {
                realTimeDataSourceList.Add(new IB(Properties.Settings.Default.ibClientHost, Properties.Settings.Default.ibClientPort, Properties.Settings.Default.rtdClientIBID));
            }

            var historicalDataSourceList = new List<IHistoricalDataSource>
            {
                new Yahoo(),
                new FRED(),
                //new Forexite(),
            };
            if (!string.IsNullOrEmpty(Properties.Settings.Default.ibClientHost) &&
                Properties.Settings.Default.ibClientPort != default(int) &&
                Properties.Settings.Default.histClientIBID != default(int))
            {
                historicalDataSourceList.Add(new IB(Properties.Settings.Default.ibClientHost, Properties.Settings.Default.ibClientPort, Properties.Settings.Default.histClientIBID));
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.quandlAuthCode))
            {
                historicalDataSourceList.Add(new Quandl(Properties.Settings.Default.quandlAuthCode));
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.barChartApiKey))
            {
                historicalDataSourceList.Add(new BarChart(Properties.Settings.Default.barChartApiKey));
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.eODDataUserName) &&
                !string.IsNullOrEmpty(Properties.Settings.Default.eODDataPassword))
            {
                historicalDataSourceList.Add(new EOD(Properties.Settings.Default.eODDataUserName, Properties.Settings.Default.eODDataPassword));
            }

            RealTimeBroker = new RealTimeDataBroker(cfRealtimeBroker, localStorage, realTimeDataSourceList);
            HistoricalBroker = new HistoricalDataBroker(cfHistoricalBroker, localStorage, historicalDataSourceList);

            var countryCodeHelper = new CountryCodeHelper(entityContext.Countries.ToList());

            EconomicReleaseBroker = new EconomicReleaseBroker("FXStreet",
                new[] { new fx.FXStreet(countryCodeHelper) });

            //create the various servers
            _realTimeServer = new RealTimeDataServer(Properties.Settings.Default.rtDBPubPort, Properties.Settings.Default.rtDBReqPort, RealTimeBroker);
            _historicalDataServer = new HistoricalDataServer(Properties.Settings.Default.hDBPort, HistoricalBroker);

            //and start them
            _realTimeServer.StartServer();
            _historicalDataServer.StartServer();
            
=======
            entityContext.Dispose();

>>>>>>> qusma/master
            //we also need a client to make historical data requests with
            _client = new QDMSClient.QDMSClient(
                "SERVERCLIENT",
                "localhost",
                Properties.Settings.Default.rtDBReqPort,
                Properties.Settings.Default.rtDBPubPort,
                Properties.Settings.Default.hDBPort,
                Properties.Settings.Default.httpPort,
                Properties.Settings.Default.apiKey,
                useSsl: Properties.Settings.Default.useSsl);
            _client.HistoricalDataReceived += _client_HistoricalDataReceived;
            _client.Error += (s, e) => _clientLogger.Error(e.ErrorMessage);

            //Create ViewModel
            ViewModel = new MainViewModel(_client, DialogCoordinator.Instance);
            DataContext = ViewModel;

            ShowChangelog();
        }

        private void AppDomain_CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Error((Exception)e.ExceptionObject, "Unhandled exception");
        }

        private void ShowChangelog()
        {
            if (ApplicationDeployment.IsNetworkDeployed &&
                ApplicationDeployment.CurrentDeployment.IsFirstRun)
            {
                var window = new ChangelogWindow();
                window.Show();
            }
        }

        //creates a context menu to set tags on instruments
        private void BuildTagContextMenu(IEnumerable<Tag> tags)
        {
            var tagMenu = (MenuItem)Resources["InstrumentTagMenu"];
            tagMenu.Items.Clear();

            foreach (Tag t in tags)
            {
                var button = new MenuItem
                {
                    Header = t.Name,
                    Tag = t.ID,
                    IsCheckable = true,
                    Style = (Style)Resources["TagCheckStyle"]
                };

                button.Click += SetTag_ItemClick;
                tagMenu.Items.Add(button);
            }
            tagMenu.Items.Add(Resources["NewTagMenuItem"]);
        }

        private void SetLogDirectory()
        {
            if (Directory.Exists(Properties.Settings.Default.logDirectory))
            {
                ((FileTarget)LogManager.Configuration.FindTargetByName("default")).FileName = Properties.Settings.Default.logDirectory + "Log.log";
            }
        }

        private void CheckDBConnection()
        {
            //if no db type has been selected, we gotta show that window no matter what
            if (Properties.Settings.Default.databaseType != "MySql" && Properties.Settings.Default.databaseType != "SqlServer")
            {
                var dbDetailsWindow = new DBConnectionWindow();
                dbDetailsWindow.ShowDialog();
            }

            if (Properties.Settings.Default.databaseType == "MySql")
            {
                //try to establish a database connection. If not possible, prompt the user to enter details
                var connection = DBUtils.CreateMySqlConnection(noDB: true);
                try
                {
                    connection.Open();
                }
                catch (Exception)
                {
                    var dbDetailsWindow = new DBConnectionWindow();
                    dbDetailsWindow.ShowDialog();
                }
                connection.Close();
            }
            else //SQL Server
            {
                //try to establish a database connection. If not possible, prompt the user to enter details
                var connection = DBUtils.CreateSqlServerConnection(noDB: true, useWindowsAuthentication: Properties.Settings.Default.sqlServerUseWindowsAuthentication);
                try
                {
                    connection.Open();
                }
                catch (Exception)
                {
                    var dbDetailsWindow = new DBConnectionWindow();
                    dbDetailsWindow.ShowDialog();
                }
                connection.Close();
            }
        }

        private void _client_HistoricalDataReceived(object sender, HistoricalDataEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
                {
                    _progressBar.Value++;
                    if (_progressBar.Value >= _progressBar.Maximum)
                    {
                        _progressBar.Value = 0;
                        _progressBar.Maximum = 0;
                        StatusBarLabel.Content = "Historical data update complete";
                    }
                    else
                    {
                        StatusBarLabel.Content = string.Format("Rcvd {0} bars of {1} @ {2}",
                            e.Data.Count,
                            e.Request.Instrument.Symbol,
                            e.Request.Frequency);
                    }
                }
                );
        }

        //check the latest date we have available in local storage, then request historical data from that date to the current time
        private void UpdateHistoricalDataBtn_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var frequency = (BarSize)((MenuItem)sender).Tag;
            List<Instrument> selectedInstruments = InstrumentsGrid.SelectedItems.Cast<Instrument>().ToList();

            int requestCount = 0;

            using (var localStorage = DataStorageFactory.Get())
            {
                foreach (Instrument i in selectedInstruments)
                {
                    if (!i.ID.HasValue) continue;
                    //TODO add GetStorageInfo to client, then remove dependency on DataStorageFactory here
                    var storageInfo = localStorage.GetStorageInfo(i.ID.Value);
                    if (storageInfo.Any(x => x.Frequency == frequency))
                    {
                        var relevantStorageInfo = storageInfo.First(x => x.Frequency == frequency);
                        _client.RequestHistoricalData(new HistoricalDataRequest(
                            i,
                            frequency,
                            relevantStorageInfo.LatestDate + frequency.ToTimeSpan(),
                            DateTime.Now,
                            dataLocation: DataLocation.ExternalOnly,
                            saveToLocalStorage: true));
                        requestCount++;
                    }
                }
            }

            if (_progressBar.Value >= _progressBar.Maximum)
            {
                _progressBar.Maximum = requestCount;
                _progressBar.Value = 0;
            }
            else
            {
                _progressBar.Maximum += requestCount;
            }
        }

        //the application is closing, shut down all the servers and stuff
        private void DXWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //save grid layout
            using (StreamWriter file = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "GridLayout.xml"))
            {
                InstrumentsGrid.SerializeLayout(file);
            }

            //Dispose main viewmodel
            ViewModel.Dispose();

            //then take down the client, the servers, and the brokers
            _client.Disconnect();
            _client.Dispose();
        }

        //exiting the application
        private void BtnExit_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            Close();
        }

        //show the interactive brokers add instrument window
        private void AddInstrumentIBBtn_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var window = new AddInstrumentInteractiveBrokersWindow(_client);

            if (window.ViewModel != null && window.ViewModel.AddedInstruments != null)
            {
                foreach (Instrument i in window.ViewModel.AddedInstruments)
                {
                    ViewModel.Instruments.Add(i);
                }
                window.Close();
            }
        }

        //show the Quandl add instrument window
        private void AddInstrumentQuandlBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new AddInstrumentQuandlWindow(_client);
            window.ShowDialog();

            if (window.ViewModel.AddedInstruments != null)
            {
                foreach (Instrument i in window.ViewModel.AddedInstruments)
                {
                    ViewModel.Instruments.Add(i);
                }
                window.Close();
            }
        }

        //show the FRED add instrument window
        private void AddInstrumentFredBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new AddInstrumentFredWindow(_client);
            window.ShowDialog();

            if (window.ViewModel.AddedInstruments != null)
            {
                foreach (Instrument i in window.ViewModel.AddedInstruments)
                {
                    ViewModel.Instruments.Add(i);
                }
                window.Close();
            }
        }

        //show the window to add a new custom futures contract
        private void BtnAddCustomFutures_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var window = new AddInstrumentManuallyWindow(_client, addingContFut: true);
            window.ShowDialog();
            if (window.ViewModel.AddedInstrument != null)
            {
                ViewModel.Instruments.Add(window.ViewModel.AddedInstrument);
            }
            window.Close();
        }

        private void EditDataBtn_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var selectedInstruments = InstrumentsGrid.SelectedItems;
            if (selectedInstruments.Count != 1) return;

            var selectedInstrument = (Instrument)selectedInstruments[0];
            var window = new DataEditWindow(selectedInstrument);
            window.ShowDialog();
        }

        private void ImportDataBtn_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var selectedInstruments = InstrumentsGrid.SelectedItems;
            if (selectedInstruments.Count != 1) return;

            var selectedInstrument = (Instrument)selectedInstruments[0];
            var window = new DataImportWindow(selectedInstrument);
            window.ShowDialog();
        }

        private void ExchangesBtn_OnItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var window = new ExchangesWindow(_client);
            window.ShowDialog();
        }

        private void SessionTemplateBtn_OnItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var window = new SessionTemplatesWindow(_client);
            window.ShowDialog();
            BuildSetSessionTemplateMenu();
        }

        private void RootSymbolsBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new RootSymbolsWindow(_client);
            window.ShowDialog();
        }

        private void PBar_Loaded(object sender, RoutedEventArgs e)
        {
            _progressBar = (ProgressBar)sender;
        }

        //delete data from selected instruments
        private void ClearDataBtn_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var selectedInstruments = InstrumentsGrid.SelectedItems;
            if (selectedInstruments.Count == 0) return;

            if (selectedInstruments.Count == 1)
            {
                var inst = (Instrument)selectedInstruments[0];
                MessageBoxResult res = MessageBox.Show(string.Format("Are you sure you want to delete all data from {0} @ {1}?", inst.Symbol, inst.Datasource.Name),
                    "Delete", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.No) return;
            }
            else
            {
                MessageBoxResult res = MessageBox.Show(string.Format("Are you sure you want to delete all data from {0} instruments?", selectedInstruments.Count),
                    "Delete", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.No) return;
            }

            using (var storage = DataStorageFactory.Get())
            {
                //todo remove dependency on local storage here, use client instead
                foreach (Instrument i in selectedInstruments)
                {
                    try
                    {
                        storage.DeleteAllInstrumentData(i);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }

            StatusBarLabel.Content = "Instrument data deleted";
        }

        //adds or removes a tag from one or more instruments
        private async void SetTag_ItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var selectedInstruments = InstrumentsGrid.SelectedItems;
            var btn = (MenuItem)routedEventArgs.Source;
            int tagID = (int)btn.Tag;
            var tagResponse = await _client.GetTags().ConfigureAwait(true);
            if (!tagResponse.WasSuccessful)
            {
                await this.ShowMessageAsync("Error", string.Join("\n", tagResponse.Errors)).ConfigureAwait(true);
                return;
            }

            var tag = tagResponse.Result.FirstOrDefault(x => x.ID == tagID);
            if (tag == null)
            {
                await this.ShowMessageAsync("Error", "Could not find tag on the server").ConfigureAwait(true);
                return;
            }

            //one instrument selected
            foreach (Instrument instrument in selectedInstruments)
            {
                if (btn.IsChecked)
                {
                    instrument.Tags.Add(tag);
                }
                else
                {
                    instrument.Tags.Remove(tag);
                }

                await _client.UpdateInstrument(instrument).ConfigureAwait(true);
            }

            CollectionViewSource.GetDefaultView(InstrumentsGrid.ItemsSource).Refresh();
        }

        private void BtnSettings_OnItemClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog();
        }

        //tag menu is opening, populate it with all tags and set the appropriate checkbox values
        private void InstrumentTagMenu_OnSubmenuOpened(object sender, RoutedEventArgs e)
        {
            List<Instrument> selectedInstruments = InstrumentsGrid.SelectedItems.Cast<Instrument>().ToList();
            if (selectedInstruments.Count == 0)
            {
                return;
            }
            else if (selectedInstruments.Count == 1)
            {
                var instrument = (Instrument)InstrumentsGrid.SelectedItem;
                //set checkboxes on the selected tags
                var instrumentTagMenu = (MenuItem)Resources["InstrumentTagMenu"];
                foreach (MenuItem btn in instrumentTagMenu.Items)
                {
                    if (btn.Tag == null || instrument.Tags == null) continue;

                    btn.IsChecked = instrument.Tags.Any(x => x.ID == (int)btn.Tag);
                    btn.IsEnabled = true;
                }
            }
            else
            {
                var instrumentTagMenu = (MenuItem)Resources["InstrumentTagMenu"];
                foreach (MenuItem btn in instrumentTagMenu.Items)
                {
                    if (btn.Tag == null) continue;

                    int tagCount = selectedInstruments.Count(x => x.Tags != null && x.Tags.Any(y => y.ID == (int)btn.Tag));
                    if (tagCount == 0 || tagCount == selectedInstruments.Count)
                    {
                        btn.IsEnabled = true;
                        btn.IsChecked = tagCount == selectedInstruments.Count;
                    }
                    else //if tags have different values among the selected instruments, just disable the button
                    {
                        btn.IsEnabled = false;
                    }
                }
            }
        }

        //add a new tag from the context menu and then apply it to the selected instruments
        private async void NewTagTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var newTagTextBox = (TextBox)sender;

            string newTagName = newTagTextBox.Text;

            //add the tag
            var addTagResult = await _client.AddTag(new Tag() { Name = newTagName }).ConfigureAwait(true);
            if(!addTagResult.WasSuccessful)
            {
                await this.ShowMessageAsync("Error", "Could not add tag").ConfigureAwait(true);
                return;
            }
            var newTag = addTagResult.Result;

            //apply the tag to the selected instruments
            var selectedInstruments = InstrumentsGrid.SelectedItems.Cast<Instrument>();
            foreach (Instrument i in selectedInstruments)
            {
                i.Tags.Add(newTag);
                await _client.UpdateInstrument(i).ConfigureAwait(true);
            }

            //update the tag menu
            var allTags = await _client.GetTags().ConfigureAwait(true);
            if (allTags.WasSuccessful)
            {
                BuildTagContextMenu(allTags.Result);
            }

            newTagTextBox.Text = "";

            CollectionViewSource.GetDefaultView(InstrumentsGrid.ItemsSource).Refresh();
        }

        private void NewDataRequestBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (InstrumentsGrid.SelectedItem == null) return;
            var window = new HistoricalRequestWindow((Instrument)InstrumentsGrid.SelectedItem);
        }

        //enable/disable menuitems in the row context menu depending on what has been selected
        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            //horrible, but what can you do?
            bool multipleSelected = InstrumentsGrid.SelectedItems.Count > 1;
            ContextMenu menu = (ContextMenu)Resources["RowMenu"];

            ((MenuItem)menu.Items[0]).IsEnabled = !multipleSelected; //new data request
            ((MenuItem)menu.Items[4]).IsEnabled = !multipleSelected; //clone
            ((MenuItem)menu.Items[5]).IsEnabled = !multipleSelected; //import data
            ((MenuItem)menu.Items[6]).IsEnabled = !multipleSelected; //edit data
        }

        private void BackupMetadataBtn_Click(object sender, RoutedEventArgs e)
        {
            DbBackup.Backup("qdmsEntities", "qdms");
        }

        private void BackupDataBtn_Click(object sender, RoutedEventArgs e)
        {
            DbBackup.Backup("qdmsDataEntities", "qdmsdata");
        }

        private void RestoreMetadataBtn_OnClick(object sender, RoutedEventArgs e)
        {
            DbBackup.Restore("qdmsEntities", "qdms");
        }

        private void RestoreDataBtn_OnClick(object sender, RoutedEventArgs e)
        {
            DbBackup.Restore("qdmsDataEntities", "qdmsdata");
        }

        private void DataJobsBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new ScheduledJobsWindow(_client);
            window.Show();
        }

        private void AboutBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new AboutWindow();
            window.ShowDialog();
        }

        private void BuildSetSessionTemplateMenu()
        {
            var setSessionMenu = (MenuItem)Resources["InstrumentSetSessionMenu"];
            setSessionMenu.Items.Clear();
            //todo remake to use client
            using (var context = new MyDBContext())
            {
                foreach (SessionTemplate t in context.SessionTemplates.ToList())
                {
                    var button = new MenuItem
                    {
                        Header = t.Name,
                        Tag = t.ID,
                    };

                    button.Click += SetSession_ItemClick;
                    setSessionMenu.Items.Add(button);
                }
            }
        }

        private async void SetSession_ItemClick(object sender, RoutedEventArgs e)
        {
            var selectedInstruments = InstrumentsGrid.SelectedItems;
            var btn = (MenuItem)e.Source;

            int templateID = (int)btn.Tag;

            var templates = await _client.GetSessionTemplates().ConfigureAwait(true);
            if(!templates.WasSuccessful)
            {
                await this.ShowMessageAsync("Error", string.Join("\n", templates.Errors)).ConfigureAwait(true);
                return;
            }

            var template = templates.Result.FirstOrDefault(x => x.ID == templateID);
            if (template == null)
            {
                await this.ShowMessageAsync("Error", "Could not find template on the server").ConfigureAwait(true);
                return;
            }

            foreach (Instrument instrument in selectedInstruments)
            {
                instrument.SessionsSource = SessionsSource.Template;
                instrument.SessionTemplateID = templateID;

                if(instrument.Sessions == null)
                {
                    instrument.Sessions = new List<InstrumentSession>();
                }

                instrument.Sessions.Clear();

                //Add the new sessions
                foreach(TemplateSession ts in template.Sessions)
                {
                    instrument.Sessions.Add(ts.ToInstrumentSession());
                }

                //update the instruments
                await _client.UpdateInstrument(instrument).ConfigureAwait(true);
            }
        }

        private void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateHelper.InstallUpdateSyncWithInfo();
        }

        private void TagsBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new TagsWindow(_client);
            window.Show();
            BuildTagContextMenu(window.ViewModel.Tags.Select(x => x.Model));
        }

        private void TableView_RowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel.EditInstrument.Execute(InstrumentsGrid.SelectedItem as Instrument).Subscribe();
        }
    }
}