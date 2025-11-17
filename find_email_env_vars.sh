#!/bin/bash
echo "🔍 Searching for old email environment variables..."
echo ""

# Check current shell environment
echo "1️⃣ Current Shell Environment Variables:"
env | grep -iE "EmailSettings|SMTP|SENDER" || echo "   ✅ None found in current shell"
echo ""

# Check launchSettings.json
echo "2️⃣ Checking launchSettings.json:"
if [ -f "Properties/launchSettings.json" ]; then
    grep -i "EmailSettings" Properties/launchSettings.json || echo "   ✅ None found in launchSettings.json"
else
    echo "   ⚠️  launchSettings.json not found"
fi
echo ""

# Check shell profiles
echo "3️⃣ Checking Shell Profiles:"
if [ -f ~/.zshrc ]; then
    echo "   ~/.zshrc:"
    grep -i "EmailSettings" ~/.zshrc || echo "      ✅ None found"
fi
if [ -f ~/.bash_profile ]; then
    echo "   ~/.bash_profile:"
    grep -i "EmailSettings" ~/.bash_profile || echo "      ✅ None found"
fi
if [ -f ~/.bashrc ]; then
    echo "   ~/.bashrc:"
    grep -i "EmailSettings" ~/.bashrc || echo "      ✅ None found"
fi
echo ""

# Check Railway (if deployed)
echo "4️⃣ Railway Environment Variables:"
echo "   ⚠️  You need to check Railway dashboard manually:"
echo "   1. Go to railway.app"
echo "   2. Select your project"
echo "   3. Go to Variables tab"
echo "   4. Look for EmailSettings__* variables"
echo ""

echo "✅ Diagnostic complete!"
