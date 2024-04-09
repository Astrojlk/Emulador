using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.Drawing; // Adicionando referência ao namespace System.Drawing

namespace MainForm
{
    public partial class Form1 : Form
    {
        private ViGEmClient _vigemClient;

        private bool isLeftMouseButtonPressed = false;
        private bool isMouseMovementLocked = false;

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const Keys ToggleLockKey = Keys.Insert;

        private bool isRightMouseButtonPressed = false;
        private Point initialMousePosition;
        private const int MAX_MOUSE_MOVEMENT = 100;

        private static LowLevelKeyboardProc _proc;
        private static LowLevelMouseProc _mouseProc;
        private static IntPtr _hookIDKeyboard = IntPtr.Zero;
        private static IntPtr _hookIDMouse = IntPtr.Zero;

        private HashSet<Keys> pressedKeys = new HashSet<Keys>();

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole(); // Importa a função para alocar um console

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole(); // Importa a função para liberar o console

        public Form1()
        {
            InitializeComponent();
            InitializeViGEm();

            _proc = HookCallback;
            _hookIDKeyboard = SetHook(WH_KEYBOARD_LL, _proc);
            _mouseProc = MouseHookCallback;
            _hookIDMouse = SetHook(WH_MOUSE_LL, _mouseProc);

            // Tenta alocar um console
            if (AllocConsole())
            {
                Console.WriteLine("Console alocado com sucesso."); // Mensagem de debug
            }
            else
            {
                Console.WriteLine("Falha ao alocar o console."); // Mensagem de debug
            }
        }

        private IXbox360Controller _xboxController;

        private void InitializeViGEm()
        {
            try
            {
                _vigemClient = new ViGEmClient();
                _xboxController = _vigemClient.CreateXbox360Controller();
                _xboxController.Connect();
                _xboxController.AutoSubmitReport = true;
            }
            catch (VigemBusNotFoundException)
            {
                MessageBox.Show("ViGEm Bus não encontrado", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Abrir navegador com o site de download do ViGEm Bus
                AbrirNavegadorNoSiteDoViGEmBus();
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao inicializar o emulador Xbox 360: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        private void AbrirNavegadorNoSiteDoViGEmBus()
        {
            try
            {
                // URL para o site de download do ViGEm Bus
                string urlViGEmBus = "https://vigembusdriver.com/";

                // Abrir o navegador padrão com a URL especificada
                Process.Start(urlViGEmBus);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir o navegador: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IntPtr SetHook(int idHook, Delegate hookProc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(idHook, hookProc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (key == ToggleLockKey && wParam == (IntPtr)WM_KEYDOWN)
                {
                    isMouseMovementLocked = !isMouseMovementLocked;
                }

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    if (!pressedKeys.Contains(key))
                    {
                        pressedKeys.Add(key);
                        UpdateControllerState();
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    if (pressedKeys.Contains(key))
                    {
                        pressedKeys.Remove(key);
                        UpdateControllerState();
                    }
                }
            }

            return CallNextHookEx(_hookIDKeyboard, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (!isMouseMovementLocked && nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_LBUTTONUP ||
                                wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONUP))
            {
                int mouseMsg = wParam.ToInt32();

                if (mouseMsg == WM_LBUTTONDOWN)
                {
                    isLeftMouseButtonPressed = true;
                    UpdateControllerState();
                }
                else if (mouseMsg == WM_LBUTTONUP)
                {
                    isLeftMouseButtonPressed = false;
                    UpdateControllerState();
                }
                else if (mouseMsg == WM_RBUTTONDOWN)
                {
                    isRightMouseButtonPressed = true;
                    UpdateControllerState();
                }
                else if (mouseMsg == WM_RBUTTONUP)
                {
                    isRightMouseButtonPressed = false;
                    UpdateControllerState();
                }
            }

            return CallNextHookEx(_hookIDMouse, nCode, wParam, lParam);
        }

        private void UpdateControllerState()
        {
            _xboxController.SetButtonState(Xbox360Button.Y, false);
            _xboxController.SetButtonState(Xbox360Button.B, false);
            _xboxController.SetButtonState(Xbox360Button.X, false);
            _xboxController.SetButtonState(Xbox360Button.A, false);
            _xboxController.SetButtonState(Xbox360Button.LeftThumb, false);
            _xboxController.SetButtonState(Xbox360Button.LeftShoulder, false);
            _xboxController.SetButtonState(Xbox360Button.RightShoulder, false);
            _xboxController.SetButtonState(Xbox360Button.RightThumb, false);
            _xboxController.SetButtonState(Xbox360Button.Down, false);
            _xboxController.SetButtonState(Xbox360Button.Left, false);
            _xboxController.SetButtonState(Xbox360Button.Right, false);
            _xboxController.SetButtonState(Xbox360Button.Up, false);
            _xboxController.SetButtonState(Xbox360Button.Back, false);
            _xboxController.SetButtonState(Xbox360Button.Start, false);
            _xboxController.SetButtonState(Xbox360Button.Up, false);


            byte leftTrigger = 0;
            byte rightTrigger = 0;

            if (isLeftMouseButtonPressed)
            {
                leftTrigger = 255;
            }

            if (isRightMouseButtonPressed)
            {
                rightTrigger = 255;
            }

            _xboxController.SetSliderValue(Xbox360Slider.RightTrigger, leftTrigger);
            _xboxController.SetSliderValue(Xbox360Slider.LeftTrigger, rightTrigger);

            int xAxisValue = 0;
            int yAxisValue = 0;

            if (pressedKeys.Contains(Keys.W))
            {
                yAxisValue = 32767;
            }
            else if (pressedKeys.Contains(Keys.S))
            {
                yAxisValue = -32768;
            }

            if (pressedKeys.Contains(Keys.D))
            {
                xAxisValue = 32767;
            }
            else if (pressedKeys.Contains(Keys.A))
            {
                xAxisValue = -32768;
            }

            _xboxController.SetAxisValue(Xbox360Axis.LeftThumbX, (short)xAxisValue);
            _xboxController.SetAxisValue(Xbox360Axis.LeftThumbY, (short)yAxisValue);

            foreach (var key in pressedKeys)
            {
                switch (key)
                {
                    case Keys.D1:
                        _xboxController.SetButtonState(Xbox360Button.Y, true);
                        break;
                    case Keys.Space:
                        _xboxController.SetButtonState(Xbox360Button.A, true);
                        break;
                    case Keys.R:
                        _xboxController.SetButtonState(Xbox360Button.X, true);
                        break;
                    case Keys.C:
                        _xboxController.SetButtonState(Xbox360Button.B, true);
                        break;
                    case Keys.LShiftKey:
                        _xboxController.SetButtonState(Xbox360Button.LeftThumb, true);
                        break;
                    case Keys.Q:
                        _xboxController.SetButtonState(Xbox360Button.LeftShoulder, true);
                        break;
                    case Keys.E:
                        _xboxController.SetButtonState(Xbox360Button.RightShoulder, true);
                        break;
                    case Keys.V:
                        _xboxController.SetButtonState(Xbox360Button.RightThumb, true);
                        break;
                    case Keys.T:
                        _xboxController.SetButtonState(Xbox360Button.Down, true);
                        break;
                    case Keys.B:
                        _xboxController.SetButtonState(Xbox360Button.Left, true);
                        break;
                    case Keys.D4:
                        _xboxController.SetButtonState(Xbox360Button.Right, true);
                        break;
                    case Keys.Alt:
                        _xboxController.SetButtonState(Xbox360Button.Up, true);
                        break;
                    case Keys.Tab:
                        _xboxController.SetButtonState(Xbox360Button.Back, true);
                        break;
                    case Keys.Escape:
                        _xboxController.SetButtonState(Xbox360Button.Start, true);
                        break;

                }
            }

            _xboxController.SubmitReport();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnhookWindowsHookEx(_hookIDKeyboard);
            _xboxController?.Disconnect();
            _vigemClient?.Dispose();

            FreeConsole();
            base.OnFormClosing(e);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(Keys vKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);


        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public static class ConsoleManager
        {
            public static bool HasConsole
            {
                get
                {
                    // Tenta obter o handle do console associado a este processo
                    IntPtr consoleHandle = GetConsoleWindow();
                    return consoleHandle != IntPtr.Zero;
                }
            }

            [DllImport("kernel32.dll")]
            private static extern IntPtr GetConsoleWindow();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Lógica a ser executada quando o botão é clicado
        }
    }
}
