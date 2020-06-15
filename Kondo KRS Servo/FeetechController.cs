using System;
using System.Collections.Generic;
using EZ_B;

namespace Feetech_Servos {

  public class FeetechController {

    // -------- function instructions ------------------
    enum functionInstructionEnum {
      INST_PING = 0x01,
      INST_READ = 0x02,
      INST_WRITE = 0x03,
      INST_REG_WRITE = 0x04,
      INST_ACTION = 0x05,
      INST_RECOVER = 0x06,
      INST_SYNC_WRITE =0x83,
      INST_RESET = 0x0a,
    }

    // ----------- baud rates ------------------
    enum baudRateEnum {
      SCSCL_1M  = 0,
      SCSCL_0_5M  = 1,
      SCSCL_250K  = 2,
      SCSCL_128K  = 3,
      SCSCL_115200  = 4,
      SCSCL_76800 = 5,
      SCSCL_57600 = 6,
      SCSCL_38400  =7,
    }

    //-------EPROM(只读)--------
    enum epromEnum {
      SCSCL_VERSION_L = 3,
      SCSCL_VERSION_H = 4,
    }

    //-------EPROM(读写)--------
    readonly byte SCSCL_ID = 5;
    readonly byte SCSCL_BAUD_RATE = 6;
    readonly byte SCSCL_RETURN_DELAY_TIME = 7;
    readonly byte SCSCL_RETURN_LEVEL =8;
    readonly byte SCSCL_MIN_ANGLE_LIMIT_L =9;
    readonly byte SCSCL_MIN_ANGLE_LIMIT_H =10;
    readonly byte SCSCL_MAX_ANGLE_LIMIT_L =11;
    readonly byte SCSCL_MAX_ANGLE_LIMIT_H = 12;
    readonly byte SCSCL_LIMIT_TEMPERATURE  =13;
    readonly byte SCSCL_MAX_LIMIT_VOLTAGE  =14;
    readonly byte SCSCL_MIN_LIMIT_VOLTAGE  =15;
    readonly byte SCSCL_MAX_TORQUE_L  =16;
    readonly byte SCSCL_MAX_TORQUE_H  =17;
    readonly byte SCSCL_ALARM_LED  =19;
    readonly byte SCSCL_ALARM_SHUTDOWN  =20;
    readonly byte SCSCL_COMPLIANCE_P  =21;
    readonly byte SCSCL_COMPLIANCE_D  =22;
    readonly byte SCSCL_COMPLIANCE_I  =23;
    readonly byte SCSCL_PUNCH_L  =24;
    readonly byte SCSCL_PUNCH_H  =25;
    readonly byte SCSCL_CW_DEAD  =26;
    readonly byte SCSCL_CCW_DEAD  =27;
    readonly byte SCSCL_OFS_L  =33;
    readonly byte SCSCL_OFS_H  =34;
    readonly byte SCSCL_MODE  =35;
    readonly byte SCSCL_MAX_CURRENT_L =36;
    readonly byte SCSCL_MAX_CURRENT_H  =37  ;

    //-------SRAM(读写)--------
    readonly byte SCSCL_TORQUE_ENABLE  =40;
    readonly byte SCSCL_GOAL_POSITION_L  =42;
    readonly byte SCSCL_GOAL_POSITION_H  =43;
    readonly byte SCSCL_GOAL_TIME_L  =44;
    readonly byte SCSCL_GOAL_TIME_H  =45;
    readonly byte SCSCL_GOAL_SPEED_L  =46;
    readonly byte SCSCL_GOAL_SPEED_H  =47;
    readonly byte SCSCL_LOCK  =48;

    //-------SRAM(只读)--------
    readonly byte SCSCL_PRESENT_POSITION_L  =56;
    readonly byte SCSCL_PRESENT_POSITION_H  =57;
    readonly byte SCSCL_PRESENT_SPEED_L  =58;
    readonly byte SCSCL_PRESENT_SPEED_H  =59;
    readonly byte SCSCL_PRESENT_LOAD_L  =60;
    readonly byte SCSCL_PRESENT_LOAD_H  =61;
    readonly byte SCSCL_PRESENT_VOLTAGE  =62;
    readonly byte SCSCL_PRESENT_TEMPERATURE  =63;
    readonly byte SCSCL_REGISTERED_INSTRUCTION  =64;
    readonly byte SCSCL_MOVING  =66;
    readonly byte SCSCL_PRESENT_CURRENT_L  =69;
    readonly byte SCSCL_PRESENT_CURRENT_H  =70;

    List<Servo.ServoPortEnum> _MOTOR_MODE_SERVOS = new List<Servo.ServoPortEnum>();

    byte[] getMasterCommand(Servo.ServoPortEnum servo, functionInstructionEnum functionInstruction) {

      List<byte> data = new List<byte>();

      byte id     = (byte)(servo - Servo.ServoPortEnum.V0);
      byte msgLen = 2; // instruction + checksum

      data.Add(0xff);
      data.Add(0xff);
      data.Add(id);

      data.Add(msgLen);

      data.Add((byte)functionInstruction);

      byte checkSum = (byte)(id + msgLen + (byte)functionInstruction);

      data.Add((byte)~checkSum);

      return data.ToArray();
    }

    byte[] getMasterCommand(Servo.ServoPortEnum servo, functionInstructionEnum functionInstruction, byte memoryRegister, params byte[] payload) {

      List<byte> data = new List<byte>();

      byte id     = (byte)(servo - Servo.ServoPortEnum.V0);
      byte msgLen = (byte)(3 + payload.Length); // instruction, register, checksum + payload

      data.Add(0xff);
      data.Add(0xff);
      data.Add(id);

      data.Add(msgLen);

      data.Add((byte)functionInstruction);

      data.Add(memoryRegister);

      data.AddRange(payload);

      byte checkSum = (byte)(id + msgLen + (byte)functionInstruction + memoryRegister);

      foreach (byte p in payload)
        checkSum += p;

      data.Add((byte)~checkSum);

      return data.ToArray();
    }

    public byte[] Ping(Servo.ServoPortEnum servo) {

      return getMasterCommand(
        servo,
        functionInstructionEnum.INST_PING);
    }

    public byte[] SetMotorOn(Servo.ServoPortEnum servo, bool status) {

      return getMasterCommand(
        servo,
        functionInstructionEnum.INST_WRITE,
        SCSCL_TORQUE_ENABLE,
        status ? (byte)1 : (byte)0);
    }

    public byte[] SetPositionLimits(Servo.ServoPortEnum servo, int minPosition, int maxPosition) {

      minPosition = Functions.Clamp(minPosition, 0, 1023);
      maxPosition = Functions.Clamp(maxPosition, 0, 1023);

      byte[] minArray = BitConverter.GetBytes((Int16)minPosition);
      byte[] maxArray = BitConverter.GetBytes((Int16)maxPosition);

      return getMasterCommand(
        servo,
        functionInstructionEnum.INST_WRITE,
        SCSCL_GOAL_POSITION_L,
        minArray[0],
        minArray[1],
        maxArray[0],
        maxArray[1]);
    }

    public byte[] SetServoMove(Servo.ServoPortEnum servo, int position) {

      position = Functions.Clamp(position, 0, 1023);

      byte[] positionArray = BitConverter.GetBytes((Int16)position);

      return getMasterCommand(
        servo,
        functionInstructionEnum.INST_WRITE,
        SCSCL_GOAL_POSITION_L,
        positionArray[1],  // 1 - lower 8 angle
        positionArray[0],  // 2 - higher 8 angle
        0, // time
        0, // time
        3,  // velocity 1000
        232 // velocity 1000
      );
    }

    public byte[] GetServoPosition(Servo.ServoPortEnum servo) {

      return new byte[0];
    }
  }
}
