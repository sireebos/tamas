
import re

def parse_price(price_str):
    if not price_str: return 0
    price_str = price_str.replace(',', '').replace(' ', '').replace('₦', '').strip()
    match = re.search(r'\d+', price_str)
    if match:
        return int(match.group())
    return 0

with open(r'c:\Users\SireTheo\Desktop\laravel\RestaurantManager\menu.docx', 'r', encoding='utf-8') as f:
    lines = f.readlines()

items = []
current_category = ""
current_subcategory = ""

for line in lines:
    line = line.strip()
    if not line: continue
    
    # Main Categories
    if "TAMA’S LOUNGE" in line: 
        current_category = "Tama's Lounge"
        current_subcategory = ""
        continue
    elif "SHAWARMA SPOT" in line:
        current_category = "Shawarma Spot"
        current_subcategory = ""
        continue
    elif "NOODLES SPOT" in line:
        current_category = "Noodles Spot"
        current_subcategory = ""
        continue
    elif "EATERY MENU" in line:
        current_category = "Eatery Menu"
        current_subcategory = ""
        continue
    elif "SWALLOW" in line:
        current_category = "Eatery Menu"
        current_subcategory = "Swallow"
        continue
    elif "BARBECUE SPOT" in line:
        current_category = "Barbecue Spot"
        current_subcategory = ""
        continue
    elif "EXTRAS" in line:
        current_category = "Extras"
        current_subcategory = ""
        continue

    # Identify subcategories (nested under main categories)
    if "Menu:" in line:
        sub = line.split(':')[0].replace("Menu", "").strip()
        # If we are in "Eatery Menu", some are sub-categories
        if current_category == "Eatery Menu":
            if "Drink" in sub: current_subcategory = "Drinks"
            elif "Pastries" in sub: current_subcategory = "Pastries"
            elif "Food" in sub: current_subcategory = "Food"
            else: current_subcategory = sub
        else:
            current_subcategory = sub
        continue

    # Identify items
    match = re.match(r'(\d+)\.\s*(.*?)[—–-]\s*(.*)', line)
    if match:
        name = match.group(2).strip()
        price = parse_price(match.group(3))
        items.append((name, price, current_category, current_subcategory))
    else:
        # Try format "Item Name - 1000"
        match = re.match(r'(.*?)[—–-]\s*(.*)', line)
        if match:
            name = match.group(1).strip()
            # If name starts with digit like "1. Name", clean it
            name = re.sub(r'^\d+\.\s*', '', name)
            price = parse_price(match.group(2))
            items.append((name, price, current_category, current_subcategory))

# Generate C# code
output = []
output.append("using RestaurantManager.Models;")
output.append("")
output.append("namespace RestaurantManager.Data;")
output.append("")
output.append("public static class MenuSeedData")
output.append("{")
output.append("    public static MenuItem[] GetMenuItems()")
output.append("    {")
output.append("        return new[]")
output.append("        {")
for name, price, cat, sub in items:
    sub_str = f', SubCategory = "{sub}"' if sub else ""
    # Escape quotes in names
    name_esc = name.replace('"', '\\"')
    output.append(f'            new MenuItem {{ Name = "{name_esc}", Category = "{cat}"{sub_str}, Price = {price}m, IsAvailable = true }},')
output.append("        };")
output.append("    }")
output.append("")
output.append("    public static InventoryItem[] GetInventoryItems()")
output.append("    {")
output.append("        return new[]")
output.append("        {")
for name, price, cat, sub in items:
    sub_str = f', SubCategory = "{sub}"' if sub else ""
    name_esc = name.replace('"', '\\"')
    output.append(f'            new InventoryItem {{ Name = "{name_esc}", Category = "{cat}"{sub_str}, CostPrice = {price}m, Quantity = 100, Unit = "pcs", MinLevel = 10, ReorderQuantity = 50 }},')
output.append("        };")
output.append("    }")
output.append("}")

with open(r'c:\Users\SireTheo\Desktop\laravel\RestaurantManager\Data\MenuSeedData.cs', 'w', encoding='utf-8') as f:
    f.write('\n'.join(output))
