﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (C) 2007-2014 ShareX Developers

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using HelpersLib;
using ScreenCaptureLib;
using ShareX.Properties;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShareX
{
    public class ScreenRecordForm : TrayForm
    {
        public bool IsRecording { get; private set; }

        private static ScreenRecordForm instance;

        public static ScreenRecordForm Instance
        {
            get
            {
                if (instance == null || instance.IsDisposed)
                {
                    instance = new ScreenRecordForm();
                    instance.Show();
                }

                return instance;
            }
        }

        private ScreenRecorder screenRecorder;
        private Rectangle captureRectangle;
        private ScreenRegionForm regionForm;
        private DWMManager dwmManager;

        private ScreenRecordForm()
        {
            TrayIcon.Text = "ShareX";
            TrayIcon.MouseClick += TrayIcon_MouseClick;
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                StopRecording();
            }
        }

        private void SelectRegion()
        {
            if (TaskHelpers.SelectRegion(out captureRectangle) && !captureRectangle.IsEmpty)
            {
                captureRectangle = CaptureHelpers.EvenRectangleSize(captureRectangle);
            }
        }

        private void ActiveWindowRegion(TaskSettings taskSettings)
        {
            IntPtr handle = NativeMethods.GetForegroundWindow();

            if (handle.ToInt32() > 0)
            {
                if (taskSettings.CaptureSettings.CaptureClientArea)
                {
                    captureRectangle = NativeMethods.GetClientRect(handle);
                }
                else
                {
                    captureRectangle = CaptureHelpers.GetWindowRectangle(handle);
                }
            }
            else
            {
                SelectRegion();
            }
        }

        public void StartRecording(TaskSettings TaskSettings)
        {
            if (TaskSettings.CaptureSettings.RunScreencastCLI)
            {
                if (!Program.Settings.VideoEncoders.IsValidIndex(TaskSettings.CaptureSettings.VideoEncoderSelected))
                {
                    MessageBox.Show("There is no valid CLI video encoder selected.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                else if (!Program.Settings.VideoEncoders[TaskSettings.CaptureSettings.VideoEncoderSelected].IsValid())
                {
                    MessageBox.Show("CLI video encoder file does not exist: " + Program.Settings.VideoEncoders[TaskSettings.CaptureSettings.VideoEncoderSelected].Path,
                        Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (TaskSettings.CaptureSettings.ScreenRecordOutput == ScreenRecordOutput.FFmpeg && !File.Exists(TaskSettings.CaptureSettings.FFmpegOptions.CLIPath))
            {
                string ffmpegText = string.IsNullOrEmpty(TaskSettings.CaptureSettings.FFmpegOptions.CLIPath) ? "ffmpeg.exe" : TaskSettings.CaptureSettings.FFmpegOptions.CLIPath;

                if (MessageBox.Show(ffmpegText + " does not exist." + Environment.NewLine + Environment.NewLine + "Would you like to automatically download it?",
                    Application.ProductName + " - Missing ffmpeg.exe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    if (FFmpegHelper.DownloadFFmpeg(false, DownloaderForm_InstallRequested) == DialogResult.OK)
                    {
                        Program.DefaultTaskSettings.CaptureSettings.FFmpegOptions.CLIPath = TaskSettings.TaskSettingsReference.CaptureSettings.FFmpegOptions.CLIPath =
                           TaskSettings.CaptureSettings.FFmpegOptions.CLIPath = Path.Combine(Program.ToolsFolder, "ffmpeg.exe");
                    }
                }
                else
                {
                    return;
                }
            }

            if (TaskSettings.AdvancedSettings.ScreenRecorderUseActiveWindow)
            {
                ActiveWindowRegion(TaskSettings);
            }
            else
            {
                SelectRegion();
            }

            if (IsRecording || captureRectangle.IsEmpty || screenRecorder != null)
            {
                return;
            }

            IsRecording = true;
            Screenshot.CaptureCursor = TaskSettings.CaptureSettings.ShowCursor;

            TrayIcon.Text = "ShareX - Waiting...";
            TrayIcon.Icon = Resources.control_record_yellow.ToIcon();
            TrayIcon.Visible = true;

            string path = "";

            float duration = TaskSettings.CaptureSettings.ScreenRecordFixedDuration ? TaskSettings.CaptureSettings.ScreenRecordDuration : 0;
            regionForm = ScreenRegionForm.Show(captureRectangle, StopRecording, duration);

            TaskEx.Run(() =>
            {
                try
                {
                    if (TaskSettings.CaptureSettings.ScreenRecordAutoDisableAero)
                    {
                        dwmManager = new DWMManager();
                        dwmManager.AutoDisable();
                    }

                    if (TaskSettings.CaptureSettings.ScreenRecordOutput == ScreenRecordOutput.AVI)
                    {
                        path = Path.Combine(TaskSettings.CaptureFolder, TaskHelpers.GetFilename(TaskSettings, "avi"));
                    }
                    else if (TaskSettings.CaptureSettings.ScreenRecordOutput == ScreenRecordOutput.FFmpeg)
                    {
                        path = Path.Combine(TaskSettings.CaptureFolder, TaskHelpers.GetFilename(TaskSettings, TaskSettings.CaptureSettings.FFmpegOptions.Extension));
                    }
                    else
                    {
                        path = Program.ScreenRecorderCacheFilePath;
                    }

                    ScreencastOptions options = new ScreencastOptions()
                    {
                        FFmpeg = TaskSettings.CaptureSettings.FFmpegOptions,
                        AVI = TaskSettings.CaptureSettings.AVIOptions,
                        ScreenRecordFPS = TaskSettings.CaptureSettings.ScreenRecordFPS,
                        GIFFPS = TaskSettings.CaptureSettings.GIFFPS,
                        Duration = duration,
                        OutputPath = path,
                        CaptureArea = captureRectangle,
                        DrawCursor = TaskSettings.CaptureSettings.ShowCursor
                    };

                    screenRecorder = new ScreenRecorder(options, captureRectangle, TaskSettings.CaptureSettings.ScreenRecordOutput);

                    int delay = (int)(TaskSettings.CaptureSettings.ScreenRecordStartDelay * 1000);

                    if (delay > 0)
                    {
                        Thread.Sleep(delay);
                    }

                    TrayIcon.Text = "ShareX - Click tray icon to stop recording.";
                    TrayIcon.Icon = Resources.control_record.ToIcon();

                    if (regionForm != null)
                    {
                        this.InvokeSafe(() => regionForm.StartTimer());
                    }

                    screenRecorder.StartRecording();
                }
                finally
                {
                    if (dwmManager != null)
                    {
                        dwmManager.Dispose();
                        dwmManager = null;
                    }

                    if (regionForm != null)
                    {
                        this.InvokeSafe(() => regionForm.Close());
                    }
                }

                try
                {
                    if (screenRecorder != null)
                    {
                        TrayIcon.Text = "ShareX - Encoding...";
                        TrayIcon.Icon = Resources.camcorder__pencil.ToIcon();

                        string sourceFilePath = path;

                        if (TaskSettings.CaptureSettings.ScreenRecordOutput == ScreenRecordOutput.GIF)
                        {
                            if (TaskSettings.CaptureSettings.RunScreencastCLI)
                            {
                                sourceFilePath = Path.ChangeExtension(Program.ScreenRecorderCacheFilePath, "gif");
                            }
                            else
                            {
                                sourceFilePath = path = Path.Combine(TaskSettings.CaptureFolder, TaskHelpers.GetFilename(TaskSettings, "gif"));
                            }

                            screenRecorder.SaveAsGIF(sourceFilePath, TaskSettings.ImageSettings.ImageGIFQuality);
                        }

                        if (TaskSettings.CaptureSettings.RunScreencastCLI)
                        {
                            VideoEncoder encoder = Program.Settings.VideoEncoders[TaskSettings.CaptureSettings.VideoEncoderSelected];
                            path = Path.Combine(TaskSettings.CaptureFolder, TaskHelpers.GetFilename(TaskSettings, encoder.OutputExtension));
                            screenRecorder.EncodeUsingCommandLine(encoder, sourceFilePath, path);
                        }
                    }
                }
                finally
                {
                    if (screenRecorder != null)
                    {
                        if (TaskSettings.CaptureSettings.RunScreencastCLI && !string.IsNullOrEmpty(screenRecorder.CachePath) && File.Exists(screenRecorder.CachePath))
                        {
                            File.Delete(screenRecorder.CachePath);
                        }

                        screenRecorder.Dispose();
                        screenRecorder = null;
                    }
                }
            },
            () =>
            {
                if (TrayIcon.Visible)
                {
                    TrayIcon.Visible = false;
                }

                IsRecording = false;

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    UploadTask task = UploadTask.CreateFileJobTask(path, TaskSettings);
                    TaskManager.Start(task);
                }
            });
        }

        private void DownloaderForm_InstallRequested(string filePath)
        {
            string extractPath = Path.Combine(Program.ToolsFolder, "ffmpeg.exe");
            bool result = FFmpegHelper.ExtractFFmpeg(filePath, extractPath);

            if (result)
            {
                MessageBox.Show("FFmpeg successfully downloaded.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Download of FFmpeg failed.", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StopRecording()
        {
            if (IsRecording && screenRecorder != null)
            {
                screenRecorder.StopRecording();
            }
        }
    }
}