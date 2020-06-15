using System;
using System.Collections.Generic;
using System.Windows.Forms;
using EZ_B;
using ARC;
using System.Linq;
using System.IO.Ports;

namespace Feetech_Servos {

  public partial class FormMain : ARC.UCForms.FormPluginMaster {

    CustomConfig        _customConfig = new CustomConfig();
    FeetechController _controller   = new FeetechController();
    SerialPort          _serialPort   = new SerialPort();

    public FormMain() {

      InitializeComponent();

      // Bind to the events for moving a servo and changing connection state
      EZBManager.EZBs[0].OnConnectionChange += FormMain_OnConnectionChange;
      EZBManager.EZBs[0].Servo.OnServoMove += Servo_OnServoMove;
      EZBManager.EZBs[0].Servo.OnServoGetPosition += Servo_OnServoGetPosition;
      EZBManager.EZBs[0].Servo.OnServoRelease += Servo_OnServoRelease;

      Invokers.SetAppendText(tbLog, true, "Connected Events");
    }

    void FormMain_OnConnectionChange(bool isConnected) {

      // If the connection is established, send an initialization to the ez-b for the uart which we will be using
      if (isConnected)
        initUART();
    }

    private void FormMain_FormClosing(object sender, FormClosingEventArgs e) {

      EZBManager.EZBs[0].OnConnectionChange -= FormMain_OnConnectionChange;
      EZBManager.EZBs[0].Servo.OnServoMove -= Servo_OnServoMove;
      EZBManager.EZBs[0].Servo.OnServoGetPosition -= Servo_OnServoGetPosition;
      EZBManager.EZBs[0].Servo.OnServoRelease -= Servo_OnServoRelease;

      if (_serialPort.IsOpen)
        _serialPort.Close();

      _serialPort.Dispose();
    }

    public override void SetConfiguration(ARC.Config.Sub.PluginV1 cf) {

      cf.STORAGE.AddIfNotExist(ConfigTitles.HARDWARE_PORT, 0);
      cf.STORAGE.AddIfNotExist(ConfigTitles.USE_HARDWARE_UART, true);
      cf.STORAGE.AddIfNotExist(ConfigTitles.USE_COM_PORT, false);
      cf.STORAGE.AddIfNotExist(ConfigTitles.COM_PORT, string.Empty);

      _customConfig = (CustomConfig)cf.GetCustomObjectV2(_customConfig.GetType());

      base.SetConfiguration(cf);

      initUART();
    }

    private void initUART() {

      if (Convert.ToBoolean(_cf.STORAGE[ConfigTitles.USE_HARDWARE_UART]) && EZBManager.PrimaryEZB.Firmware.IsCapabilitySupported(EZ_B.Firmware.XMLFirmwareSimulator.CAP_HARDWARE_UART_TX_RX_WITH_DMA_BUFFER)) {

        if (EZBManager.EZBs[0].IsConnected) {

          UInt32 baud = 1000000;
          int uartPort = Convert.ToInt16(_cf.STORAGE[ConfigTitles.HARDWARE_PORT]);

          Invokers.SetAppendText(tbLog, true, "UART {0} @ {1}bps",
            uartPort,
            baud);

          EZBManager.EZBs[0].Uart.UARTExpansionInit(uartPort, baud);
        }
      }

      if (Convert.ToBoolean(_cf.STORAGE[ConfigTitles.USE_COM_PORT])) {

        if (_serialPort.IsOpen)
          _serialPort.Close();

        _serialPort.BaudRate = 1000000;
        _serialPort.PortName = _cf.STORAGE[ConfigTitles.COM_PORT].ToString();

        Invokers.SetAppendText(tbLog, true, "{0} @ {1}bps",
          _serialPort.PortName,
          _serialPort.BaudRate);

        try {

          _serialPort.Open();
        } catch (Exception ex) {

          Invokers.SetAppendText(tbLog, true, ex.Message);
        }
      }
    }

    public override object[] GetSupportedControlCommands() {

      return new string[] {
        string.Format("\"{0}\", [Virtual Servo Port], [0 to 1023]", ControlCommands.SET_POSITION_RAW),
        };
    }

    public override void SendCommand(string windowCommand, params string[] values) {

      if (windowCommand.Equals(ControlCommands.SET_POSITION_RAW, StringComparison.InvariantCultureIgnoreCase)) {

        if (values.Length != 2)
          throw new Exception("Expecting 2 parameters, which are the virtual servo port and position");

        var port = (Servo.ServoPortEnum)Enum.Parse(typeof(Servo.ServoPortEnum), values[0], true);

        int position = Convert.ToInt32(values[1]);

        sendServoCommand(_controller.SetServoMove(port, position));
      } else {

        base.SendCommand(windowCommand, values);
      }
    }

    void Servo_OnServoMove(EZ_B.Classes.ServoPositionItem[] servos) {

      List<byte> cmdData = new List<byte>();

      foreach (var servo in servos) {

        if (servo.Port < EZ_B.Servo.ServoPortEnum.V0 || servo.Port > EZ_B.Servo.ServoPortEnum.V99)
          continue;

        if (_customConfig.VirtualPorts.Contains(servo.Port))
          cmdData.AddRange(_controller.SetServoMove(servo.Port, (int)Functions.RemapScalar(servo.Position, Servo.SERVO_MIN, Servo.SERVO_MAX, 1, 1023)));
      }

      sendServoCommand(cmdData.ToArray());
    }

    private void sendServoCommand(byte[] cmdData) {

      try {

        if (cmdData.Length == 0)
          return;

        if (Convert.ToBoolean(_cf.STORAGE[ConfigTitles.USE_HARDWARE_UART]))
          EZBManager.EZBs[0].Uart.UARTExpansionWrite(
            Convert.ToInt16(_cf.STORAGE[ConfigTitles.HARDWARE_PORT]),
            cmdData);

        if (Convert.ToBoolean(_cf.STORAGE[ConfigTitles.USE_COM_PORT]))
          _serialPort.Write(cmdData, 0, cmdData.Length);
      } catch (Exception ex) {

        Log(ex.Message);
      }
    }

    public override void ConfigPressed() {

      using (FormConfig form = new FormConfig()) {

        if (_serialPort.IsOpen)
          _serialPort.Close();

        form.SetConfiguration(_cf);

        if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
          SetConfiguration(form.GetConfiguration());
        else
          initUART();
      }
    }

    private void Servo_OnServoRelease(Servo.ServoPortEnum[] servos) {

      List<byte> cmdData = new List<byte>();

      foreach (var servo in servos) {

        if (servo < EZ_B.Servo.ServoPortEnum.V0 || servo > EZ_B.Servo.ServoPortEnum.V99)
          continue;

        if (_customConfig.VirtualPorts.Contains(servo))
          cmdData.AddRange(_controller.SetMotorOn(servo, false));
      }

      sendServoCommand(cmdData.ToArray());
    }

    private void Servo_OnServoGetPosition(Servo.ServoPortEnum servoPort, EZ_B.Classes.GetServoValueResponse getServoResponse) {

      if (getServoResponse.Success)
        return;

      if (!_customConfig.VirtualPorts.Contains(servoPort)) {

        getServoResponse.ErrorStr = "No matching lewansoul servo specified";
        getServoResponse.Success = false;

        return;
      }

      Invokers.SetAppendText(tbLog, true, "Reading position from {0}", servoPort);

      if (Convert.ToBoolean(_cf.STORAGE[ConfigTitles.USE_COM_PORT]))
        getServoPositionComSerial(servoPort, getServoResponse);

      if (!getServoResponse.Success && Convert.ToBoolean(_cf.STORAGE[ConfigTitles.USE_HARDWARE_UART]))
        getServoPositionEZBUART(servoPort, getServoResponse);
    }

    private void getServoPositionComSerial(Servo.ServoPortEnum servoPort, EZ_B.Classes.GetServoValueResponse getServoResponse) {

      if (!_serialPort.IsOpen) {

        getServoResponse.Success = false;
        getServoResponse.ErrorStr = "COM Port not open";

        return;
      }

      _serialPort.DiscardInBuffer();

      sendServoCommand(_controller.GetServoPosition(servoPort));

      System.Threading.Thread.Sleep(100);

      var ret = new byte[_serialPort.BytesToRead];

      _serialPort.Read(ret, 0, ret.Length);

      if (ret.Length != 14) {

        getServoResponse.ErrorStr = "Servo did not respond";
        getServoResponse.Success = false;

        return;
      }

      getServoResponse.Position = (int)EZ_B.Functions.RemapScalar(BitConverter.ToInt16(ret, 11), 1, 1000, Servo.SERVO_MIN, Servo.SERVO_MAX);
      getServoResponse.Success = true;
    }

    private void getServoPositionEZBUART(Servo.ServoPortEnum servoPort, EZ_B.Classes.GetServoValueResponse getServoResponse) {

      if (!EZBManager.EZBs[0].IsConnected) {

        getServoResponse.Success = false;
        getServoResponse.ErrorStr = "Not connected to EZ-B 0";

        return;
      }

      if (!Convert.ToBoolean(_cf.STORAGE[ConfigTitles.USE_HARDWARE_UART])) {

        getServoResponse.ErrorStr = "This feature is only available when using the hardware uart";
        getServoResponse.Success = false;

        return;
      }

      initUART();

      sendServoCommand(_controller.GetServoPosition(servoPort));

      System.Threading.Thread.Sleep(100);

      var ret = EZBManager.EZBs[0].Uart.UARTExpansionReadAvailable(Convert.ToInt16(_cf.STORAGE[ConfigTitles.HARDWARE_PORT]));

      if (ret.Length != 14) {

        getServoResponse.ErrorStr = "Servo did not respond";
        getServoResponse.Success = false;

        return;
      }

      getServoResponse.Position = (int)EZ_B.Functions.RemapScalar(BitConverter.ToInt16(ret.Reverse().ToArray(), 0), 1, 1000, Servo.SERVO_MIN, Servo.SERVO_MAX);
      getServoResponse.Success = true;
    }

    private void button1_Click(object sender, EventArgs e) {

      sendServoCommand(_controller.SetMotorOn(Servo.ServoPortEnum.V1, true));

      //      sendServoCommand(_controller.Ping(Servo.ServoPortEnum.V1));
    }
  }
}
