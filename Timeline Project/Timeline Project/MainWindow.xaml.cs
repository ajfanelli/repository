﻿using System;
using System.IO;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using TimelineLib;
using TimelineLib.Themes;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using Newtonsoft.Json;

using Color = SFML.Graphics.Color;
using Mouse = SFML.Window.Mouse;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Timeline_Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private RenderWindow _renderWindow;
        private TimelineViewModel model;

        public bool KeyPressed_W;
        public bool KeyPressed_A;
        public bool KeyPressed_S;
        public bool KeyPressed_D;
        public bool KeyPressed_CTRL;

        public bool IsMouseDown = false;

        public Vector2f PrevMousePos;
        public Vector2f LastClickedPos;

        private Stopwatch RefreshScreenStopwatch;
        public float RefreshRateInSeconds = 0.5f;

        public Stopwatch DisplayEventNoteStopwatch;


        public MainWindow()
        {
            InitializeComponent();

            // Load a new Timeline
            LoadTimeline();

            // Initialize a test category
            model.AddCategory(new Category()
            {
                BackgroundColor = new Color(139, 201, 201),
                BackgroundColorHover = new Color(139, 201, 201) + new Color(40, 40, 40),
                TextColor = Color.Black,
                ID = 1,
                Name = "Blue Category",
                PriorityLevel = 1
            });

            // Initialize a test event and a test timespan
            model.AddEvent(new EventViewModel().SetViewModel(new EventModel()
            {
                CategoryID = 1,
                Name = "Whoa, it's an event",
                StartYear = 20,
                Note = "That one time when the thing was at the place"
            }));

            model.AddEvent(new EventViewModel().SetViewModel(new EventModel()
            {
                CategoryID = 1,
                Name = "Whoa, it's a TIMESPAN",
                StartYear = 30,
                EndYear = 50,
                Note = "OOOOGA       B O O G A"
            }));

            DisplayEventNoteStopwatch = new Stopwatch();
            RefreshScreenStopwatch = new Stopwatch();
        }

        private void CreateRenderWindow()
        {
            if (this._renderWindow != null)
            {
                this._renderWindow.SetActive(false);
                this._renderWindow.Dispose();
            }

            var context = new ContextSettings { DepthBits = 24, AntialiasingLevel = 50};
            this._renderWindow = new RenderWindow(DrawSurface.Handle, context);
            this._renderWindow.SetActive(true);

            UpdateWindow();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWindow();

            RefreshScreenStopwatch.Start();

            Thread backgroundThread = new Thread(() =>
                {
                    while (_renderWindow.IsOpen)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(DispatcherPriority.Loaded, new Action(() =>
                        {
                            // Update Year At Mouse
                            model.YearAtMouse = (int)Math.Round(
                                (Mouse.GetPosition().X - _renderWindow.Position.X - model.OffsetX) / (model.IntervalLengthPx * model.Zoom));

                            bool Update;

                            if(RefreshScreenStopwatch.ElapsedMilliseconds > RefreshRateInSeconds * 1000)
                            {
                                Update = true;
                                RefreshScreenStopwatch.Restart();
                            }
                            else
                            {
                                Update =
                                    IsMouseDown ||
                                    KeyPressed_W || KeyPressed_A || KeyPressed_S || KeyPressed_D;
                            }


                            // Check if a note needs to be drawn

                            bool DrawNote = false;
                            foreach (EventViewModel n in model.ListOfEvents)
                            {
                                if (n.IsMouseOver(_renderWindow))
                                {
                                    // Update window so the background color changes
                                    Update = true;

                                    // Check if Note needs to be drawn
                                    if (n.Note != null)
                                    {
                                        DrawNote = true;

                                        if (!DisplayEventNoteStopwatch.IsRunning) DisplayEventNoteStopwatch.Start();

                                        if (DisplayEventNoteStopwatch.Elapsed.TotalSeconds > model.DisplayNoteDelayInSeconds)
                                        {
                                            model.EventToDrawNote = n;
                                        }
                                    }
                                }
                            }

                            if (!DrawNote)
                            {
                                DisplayEventNoteStopwatch.Stop();
                                DisplayEventNoteStopwatch.Reset();

                                model.EventToDrawNote = null;
                            }

                            if (Update) UpdateWindow();
                        }
                        ));
                    }
                });

            backgroundThread.Start();

            DrawSurface.Focus();
        }

        public void LoadTimeline(TimelineViewModel timelineViewModel = null)
        {
            // If no viewmodel is given, create a new viewmodel
            if(timelineViewModel == null)
            {
                timelineViewModel = new TimelineViewModel();

                timelineViewModel.SetViewModel(new TimelineModel()
                {
                    Title = "New Timeline",
                    ThemeID = 3
                });
            }

            if(this.model != null) 
                this.model = null;

            this.model = timelineViewModel;

            // Set the XAML DataContext
            this.DataContext = model;

            // Create the Window
            this.CreateRenderWindow();

            // Set the Zoom
            this.ResetZoom();
        }

        private void UpdateWindow()
        {
            this.model.Debug_NoRefreshes += 1;  // Display Number of Refreshes for Debugging

            //      Process Events
            this._renderWindow.DispatchEvents();

            //      Clear Screen
            this._renderWindow.Clear(model.Theme.BackgroundColor);

            //      Draw Screen
            // SCROLL SCREEN
            if (KeyPressed_W) model.OffsetY += model.ScrollSpeed;
            if (KeyPressed_A) model.OffsetX += model.ScrollSpeed;
            if (KeyPressed_S) model.OffsetY -= model.ScrollSpeed;
            if (KeyPressed_D) model.OffsetX -= model.ScrollSpeed;

            // DRAW LINE
            model.DrawLine(this._renderWindow);

            // DRAW MARKERS
            model.DrawMarkers(this._renderWindow);

            // DRAW EVENTS
            model.DrawEvents(this._renderWindow);

            // PAN SCREEN
            if(IsMouseDown)
            {
                float CurrMouseX = Mouse.GetPosition().X - _renderWindow.Position.X;
                float CurrMouseY = Mouse.GetPosition().Y - _renderWindow.Position.Y;

                model.OffsetX -= PrevMousePos.X - CurrMouseX;
                model.OffsetY -= PrevMousePos.Y - CurrMouseY;

                PrevMousePos = new Vector2f(CurrMouseX, CurrMouseY);
            }

            // DRAW TITLE
            model.DrawTitle(this._renderWindow);


            //      Display Window
            this._renderWindow.Display();
        }

        public void ResetZoom()
        {
            if (this.model.ListOfEvents.Count > 0)
                this.model.OffsetX = this.model.ListOfEvents.First().StartYear * this.model.IntervalLengthPx * -1;
            else
                model.OffsetX = this._renderWindow.Size.X / 2;

            model.OffsetY = 0;
            model.Zoom = 1;
            model.ScrollCount = 0;
        }

        public void OpenSideColumn(EventViewModel eventViewModel = null)
        {
            model.IsSideColumnVisible = true;

            // New Event Form
            if (eventViewModel == null)
            {
                model.SideColumnHeader = "Add New Event";
                model.EditingEventName = "";
                model.EditingEventStartYear = model.YearAtMouse;
                model.EditingEventStartMonth = 0;
                model.EditingEventStartDay = 0;
                model.EditingEventEndYear = null;
                model.EditingEventEndMonth = null;
                model.EditingEventEndDay = null;
                model.EditingEventNote = null;

                EndMonthTextBox.IsEnabled = EndDayTextBox.IsEnabled = false;
            }
            // Edit Event Form (if a model is passed in)
            else
            {
                model.SideColumnHeader = "Edit Event";
                model.EditingEventName = eventViewModel.Name;
                model.EditingEventStartYear = eventViewModel.StartYear;
                model.EditingEventStartMonth = eventViewModel.StartMonth;
                model.EditingEventStartDay = eventViewModel.StartDay;
                model.EditingEventEndYear = eventViewModel.EndYear;
                model.EditingEventEndMonth = eventViewModel.EndMonth;
                model.EditingEventEndDay = eventViewModel.EndDay;
                model.EditingEventNote = eventViewModel.Note;
                model.ShowDeleteButton = true;

                EndMonthTextBox.IsEnabled = 
                    EndDayTextBox.IsEnabled = 
                        eventViewModel.EndYear != null;

                model.EditingEvent = eventViewModel;
            }

            NameTextBox.Focus();

            UpdateWindow();
        }

        public void CloseSideColumn()
        {
            model.IsSideColumnVisible = false;

            model.ShowDeleteButton = false;
            model.EditingEvent = null;

            UpdateWindow();

            DrawSurface.Focus();
        }

        public void SaveFile()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";
            if (saveFileDialog.ShowDialog() == true)
                File.WriteAllText(saveFileDialog.FileName, JsonConvert.SerializeObject(model.ConvertToSaveModel(), Formatting.Indented));
        }

        public void OpenFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                TimelineViewModel timelineViewModel = new TimelineViewModel();
                timelineViewModel.SetViewModel(JsonConvert.DeserializeObject<TimelineModel>(json));
                this.LoadTimeline(timelineViewModel);
            }
        }

        private void DrawSurface_SizeChanged(object sender, EventArgs e)
        {
            this.CreateRenderWindow();
        }

        private void DrawSurface_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            switch (e.KeyValue)
            {
                // Navigation
                case 'W':
                    KeyPressed_W = true;
                    break;

                case 'A':
                    KeyPressed_A = true;
                    break;

                case 'S':

                    if (e.Control) {
                        e.SuppressKeyPress = true;
                        SaveFile();
                    }
                    else
                        KeyPressed_S = true;
                    break;

                case 'D':
                    KeyPressed_D = true;
                    break;

                // Keyboard Shortcuts
                case 'N':
                    e.SuppressKeyPress = true;
                    if (!model.IsSideColumnVisible) OpenSideColumn();
                    break;

                case 'R':
                    ResetZoom();
                    break;
            }
        }

        private void DrawSurface_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            switch (e.KeyValue)
            {
                case 'W':
                    KeyPressed_W = false;
                    break;

                case 'A':
                    KeyPressed_A = false;
                    break;

                case 'S':
                    KeyPressed_S = false;
                    break;

                case 'D':
                    KeyPressed_D = false;
                    break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _renderWindow.Close();
        }

        private void DrawSurface_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            model.ScrollCount += Math.Sign(e.Delta);

            //Cap Zoom
            if (model.ScrollCount < model.ZoomMaxCap) model.ScrollCount = model.ZoomMaxCap;
            if (model.ScrollCount > model.ZoomMinCap) model.ScrollCount = model.ZoomMinCap;

            double oldZoom = model.Zoom;
            model.Zoom = (float)Math.Pow(1 + (model.ZoomSpeed / 100 * Math.Sign(model.ScrollCount)), Math.Abs(model.ScrollCount));

            double oldDelta = (Mouse.GetPosition().X - this._renderWindow.Position.X) - model.OffsetX;
            double newDelta = oldDelta * (model.Zoom / oldZoom);

            model.OffsetX += (float)(oldDelta - newDelta);

            UpdateWindow();
        }

        private void DrawSurface_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            IsMouseDown = true;

            PrevMousePos = new Vector2f(Mouse.GetPosition().X - _renderWindow.Position.X, Mouse.GetPosition().Y - _renderWindow.Position.Y);
            LastClickedPos = PrevMousePos;
        }

        private void DrawSurface_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            IsMouseDown = false;
        }

        private void DrawSurface_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            bool closeSideColumn = true;

            foreach (EventViewModel eventViewModel in model.ListOfEvents)
            {
                if (eventViewModel.IsMouseOver(_renderWindow) && eventViewModel != model.EditingEvent)
                {
                    CloseSideColumn();
                    OpenSideColumn(eventViewModel);

                    closeSideColumn = false;
                }
            }

            // Close side column if the cursor wasn't dragged
            if (closeSideColumn && LastClickedPos == new Vector2f(Mouse.GetPosition().X - _renderWindow.Position.X, Mouse.GetPosition().Y - _renderWindow.Position.Y))
                CloseSideColumn();
        }

        private void DrawSurface_DoubleClick(object sender, EventArgs e)
        {
            OpenSideColumn();
        }

        private void menuItemNewEvent_Click(object sender, RoutedEventArgs e)
        {
            OpenSideColumn();
        }

        private void On_ThemeChange(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.MenuItem src = (System.Windows.Controls.MenuItem)e.Source;
            model.Theme = Theme.GetThemeByName(src.Name);
            DrawSurface.Focus();
            UpdateWindow();
        }

        private void btnSubmitNewEvent_Click(object sender, RoutedEventArgs e)
        {
            NewEventSubmitButton.Focus();
            if (model.EditingEventName != "")
            {
                SubmitNewEvent();
            }
        }

        private void SubmitNewEvent()
        {
            NewEventSubmitButton.Focus();

            if (model.EditingEvent != null)
            {
                model.EditingEvent.Name = model.EditingEventName;
                model.EditingEvent.StartYear = model.EditingEventStartYear;
                model.EditingEvent.StartMonth = model.EditingEventStartMonth;
                model.EditingEvent.StartDay = model.EditingEventStartDay;
                model.EditingEvent.EndYear = model.EditingEventEndYear;
                model.EditingEvent.EndMonth = model.EditingEventEndMonth;
                model.EditingEvent.EndDay = model.EditingEventEndDay;
                model.EditingEvent.Note = model.EditingEventNote;
            }
            else
            {
                model.AddEvent(new EventViewModel().SetViewModel(new EventModel() 
                { 
                    Name = model.EditingEventName, 
                    StartYear = model.EditingEventStartYear,
                    StartMonth = model.EditingEventStartMonth,
                    StartDay = model.EditingEventStartDay,
                    EndYear = model.EditingEventEndYear,
                    EndMonth = model.EditingEventEndMonth,
                    EndDay = model.EditingEventEndDay,
                    Note = model.EditingEventNote
                }));
            }

            CloseSideColumn();
        }
        private void btnCancelNewEvent_Click(object sender, RoutedEventArgs e)
        {
            CloseSideColumn();
        }
        
        private void btnDeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if(model.IsSideColumnVisible && model.EditingEvent != null)
            {
                model.ListOfEvents.Remove(model.EditingEvent);

                CloseSideColumn();
            }
        }

        private void TextBox_SelectAllOnFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox textbox = (System.Windows.Controls.TextBox)e.Source;
            textbox.SelectAll();
        }

        private void btnNewFile_Click(object sender, RoutedEventArgs e)
        {
            LoadTimeline();
        }

        private void btnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFile();
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }

        private void EndYearTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            EndMonthTextBox.IsEnabled = true;
            EndDayTextBox.IsEnabled = true;
        }
    }
}
