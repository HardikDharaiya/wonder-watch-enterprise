#!/bin/bash

# 1. Define the base wwwroot path
WEB_ROOT="WonderWatch.Web/wwwroot"

echo -e "\e[36mInitializing Real Luxury Asset Hydration...\e[0m"

# 2. Define the exact GUIDs from our SeedData.cs
watchIds=(
    "11111111-1111-1111-1111-111111111111"
    "22222222-2222-2222-2222-222222222222"
    "33333333-3333-3333-3333-333333333333"
    "44444444-4444-4444-4444-444444444444"
    "55555555-5555-5555-5555-555555555555"
    "66666666-6666-6666-6666-666666666666"
)

# 3. Map specific high-quality Unsplash photos to our watches
declare -A watchImages
watchImages[${watchIds[0]}]="https://images.unsplash.com/photo-1523170335258-f5ed11844a49?q=80&w=800&auto=format&fit=crop"
watchImages[${watchIds[1]}]="https://images.unsplash.com/photo-1548171915-e79a380a2a4b?q=80&w=800&auto=format&fit=crop"
watchImages[${watchIds[2]}]="https://images.unsplash.com/photo-1614164185128-e4ec99c436d7?q=80&w=800&auto=format&fit=crop"
watchImages[${watchIds[3]}]="https://images.unsplash.com/photo-1524592094714-0f0654e20314?q=80&w=800&auto=format&fit=crop"
watchImages[${watchIds[4]}]="https://images.unsplash.com/photo-1587836374828-cb43878609f7?q=80&w=800&auto=format&fit=crop"
watchImages[${watchIds[5]}]="https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?q=80&w=800&auto=format&fit=crop"

echo "Downloading Real Watch Photography..."
for id in "${watchIds[@]}"; do
    watchDir="$WEB_ROOT/images/watches/$id"
    mkdir -p "$watchDir"
    
    echo " -> Fetching image for Watch ID: $id"
    curl -sL "${watchImages[$id]}" -o "$watchDir/1.webp"
done

# Add the second image specifically for Grand Mariner III
curl -sL "https://images.unsplash.com/photo-1522312346375-d1a52e2b99b3?q=80&w=800&auto=format&fit=crop" -o "$WEB_ROOT/images/watches/${watchIds[0]}/2.webp"

# 4. Generate Editorial & Brand Images
echo "Downloading Editorial & Brand Photography..."
declare -A editorialAssets
editorialAssets["$WEB_ROOT/images/placeholder.webp"]="https://images.unsplash.com/photo-1523170335258-f5ed11844a49?q=80&w=800&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/brand/login-bg.webp"]="https://images.unsplash.com/photo-1495704907664-81f74a7efd9b?q=80&w=1080&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/brand/register-bg.webp"]="https://images.unsplash.com/photo-1612817159949-195b6eb9e31a?q=80&w=1080&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/brand/vault-entry.webp"]="https://images.unsplash.com/photo-1584916201218-f4242ceb4809?q=80&w=1080&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/brand/og-image.webp"]="https://images.unsplash.com/photo-1523170335258-f5ed11844a49?q=80&w=1200&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/collections/void-series.webp"]="https://images.unsplash.com/photo-1548171915-e79a380a2a4b?q=80&w=1000&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/collections/legacy-gold.webp"]="https://images.unsplash.com/photo-1614164185128-e4ec99c436d7?q=80&w=1000&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/collections/deep-sea.webp"]="https://images.unsplash.com/photo-1587836374828-cb43878609f7?q=80&w=1920&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/about/material-carbon.webp"]="https://images.unsplash.com/photo-1596431315800-111001421111?q=80&w=800&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/about/material-titanium.webp"]="https://images.unsplash.com/photo-1620336655055-088d06e36bf0?q=80&w=800&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/about/material-gold.webp"]="https://images.unsplash.com/photo-1614164185128-e4ec99c436d7?q=80&w=800&auto=format&fit=crop"
editorialAssets["$WEB_ROOT/images/about/atelier-dark.webp"]="https://images.unsplash.com/photo-1501166222995-ff41c7e50859?q=80&w=1920&auto=format&fit=crop"

mkdir -p "$WEB_ROOT/images/brand"
mkdir -p "$WEB_ROOT/images/collections"
mkdir -p "$WEB_ROOT/images/about"

for assetPath in "${!editorialAssets[@]}"; do
    echo " -> Fetching: $assetPath"
    curl -sL "${editorialAssets[$assetPath]}" -o "$assetPath"
done

echo -e "\e[32mReal Asset Hydration Complete! Your local environment is now fully styled.\e[0m"
