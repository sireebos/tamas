
import sys

try:
    with open(r'c:\Users\SireTheo\Desktop\laravel\RestaurantManager\menu.docx', 'rb') as f:
        content = f.read()
    
    # Try to decode as utf-8, if fails try others
    try:
        text = content.decode('utf-8')
    except UnicodeDecodeError:
        try:
            text = content.decode('latin-1')
        except UnicodeDecodeError:
            text = str(content)
            
    print(text)
except Exception as e:
    print(f"Error: {e}")
