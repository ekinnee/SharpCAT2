#!/bin/bash

# Test script to demonstrate the serial port abstraction functionality
# This tests both real and fake serial port implementations

echo "Testing SharpCAT2 Serial Port Abstraction"
echo "=========================================="

cd "$(dirname "$0")/Server"

echo ""
echo "1. Testing list of available radios..."
dotnet run -- --list-radios | grep "SharpCAT2 DummyRadio"

echo ""
echo "2. Testing DummyRadio with FakeSerialPort..."
echo "   Starting server with FAKE port and DummyRadio..."

# Create a temporary expect script to automate interaction
cat > /tmp/test_dummy.exp << 'EOF'
#!/usr/bin/expect -f
spawn dotnet run -- --port FAKE --radio "SharpCAT2 DummyRadio"
expect "Press 'q' to quit, 's' for radio status, or type radio commands/messages..."
send "ID;\r"
expect "Radio response:"
send "FA;\r" 
expect "Radio response:"
send "s\r"
expect "Radio Status:"
send "q\r"
expect eof
EOF

if command -v expect >/dev/null 2>&1; then
    chmod +x /tmp/test_dummy.exp
    /tmp/test_dummy.exp
    rm -f /tmp/test_dummy.exp
else
    echo "   Expect not available, showing manual test instructions:"
    echo "   Run: dotnet run -- --port FAKE --radio \"SharpCAT2 DummyRadio\""
    echo "   Try commands: ID; FA; s q"
fi

echo ""
echo "3. Testing SerialPortFactory functionality..."
dotnet run -c Release -- --radio-info "SharpCAT2 DummyRadio" | head -20

echo ""
echo "Testing complete!"
echo ""
echo "Summary of refactoring:"
echo "- Created ISerialPort interface for serial port abstraction"
echo "- Implemented RealSerialPort wrapper around System.IO.Ports.SerialPort"
echo "- Implemented FakeSerialPort for testing and simulation" 
echo "- Moved fake serial port logic from DummyRadio into FakeSerialPort"
echo "- Updated all radio interfaces to use ISerialPort instead of SerialPort"
echo "- Created SerialPortFactory for instantiating correct implementations"
echo "- Updated Server application to use the new abstraction"
echo "- Maintained backward compatibility while enabling testability"