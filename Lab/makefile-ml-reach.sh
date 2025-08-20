#!/bin/bash
#
#
# Copyright (c) 2025, Taiyo Hirayama
# All rights reserved.
#

graph=$1
y=$2

file_name="N2000-random-n100-A50-ML.org"

# Error-Bar Graph (E --> Attack Reach Time)
echo '
set terminal eps enhanced font "Helvetica,15
set output "'${graph}'-ml-reach.eps"
set style fill solid 1

# Set the range of the y-axis
set yrange [0:'${y}']
set ytics nomirror            # y軸の反対側（右側）のティックを消す（好みに応じて）


# Set the labels for the x and y axis
set xrange [:]
##set xtic rotate by 0
set xtics ("" 0.75)
set key right
## set xlabel "'${graph}' graph" font "Helvetica,20
set ylabel "coverage raito" font "Helvetica,20

# Set the color variable and box width
red = "#ef1b06"; green = "#03a537"; blue = "#0000FF"; skyblue = "#87CEEB"; black = "#000000"; skin= "#e07421";
orange = "#ef6106"; darkblue = "#1d7777"; purple = "#800080"; yellow = "#FFFF00"; pink = "#FFC0CB"; brown = "#8B4513"

# border
set border 3  

# edit margin and label pos
set xlabel offset 0, 2 
set margins 10, 1, 10, 3

set xtics nomirror    # 目盛線（ティックマーク）の両端表示を防ぐ
set style line 1 lt 0 # 線種を無しに設定（線を透明に）
set xtic rotate by -90
set xtics ("no-attack" 0.0 , "ml-ml" 0.20, "tri-exit" 0.40,  "loop-exit" 0.60, "cluster-exit" 0.80, "trir-bridge" 1.00,  "loop-bridge" 1.20, "cluster-bridge" 1.40)

# Set title font
set termoption font "Helvetica,15"

gray = "#808080" ;

blue1 = "#87CEEB";
blue2  = "#4169E1";
blue3= "#000080";

green1 = "#98FB98";
green2 = "#A8C98D";
green3 = "#56806F";


brown1 = "#DEB887";
brown2 = "#D2B48C";
brown3 = "#8B4513";


set offsets 0.1, 0.1, 0, 0
' > ${graph}-attack-reach.res

agent=("SRW" "SRW-ml-ml"
       "SRW-tri-exit" "SRW-loop-exit" "SRW-cluster-exit"
       "SRW-tri-bridge" "SRW-loop-bridge" "SRW-cluster-bridge")

echo -n '
# Load and plot the data
$DATA << EOD
# '${agent[0]}' '${agent[1]}' '${agent[2]}' '${agent[3]}' '${agent[4]}' '${agent[5]}' '${agent[6]}' '${agent[7]}'
' >> ${graph}-attack-reach.res


echo $graph

#grep $graph | awk '{print $4}'

# Extract and calculate for agent 1
agent_1=($(cat $file_name | grep ${agent[0]} | grep $graph | awk '{print $6}'))
agent_1=$(echo "scale=2; 1 - ($agent_1 / 2000)" | bc -l)

# Extract and calculate for agent 2
agent_2=($(cat $file_name | grep ${agent[1]} | grep $graph | awk '{print $6}'))
agent_2=$(echo "scale=2; 1 - ($agent_2 / 2000)" | bc -l)

# Extract and calculate for agent 3
agent_3=($(cat $file_name | grep ${agent[2]} | grep $graph | awk '{print $6}'))
agent_3=$(echo "scale=2; 1 - ($agent_3 / 2000)" | bc -l)

# Extract and calculate for agent 4
agent_4=($(cat $file_name | grep ${agent[3]} | grep $graph | awk '{print $6}'))
agent_4=$(echo "scale=2; 1 - ($agent_4 / 2000)" | bc -l)

# Extract and calculate for agent 5
agent_5=($(cat $file_name | grep ${agent[4]} | grep $graph | awk '{print $6}'))
agent_5=$(echo "scale=2; 1 - ($agent_5 / 2000)" | bc -l)

# Extract and calculate for agent 6
agent_6=($(cat $file_name | grep ${agent[5]} | grep $graph | awk '{print $6}'))
agent_6=$(echo "scale=2; 1 - ($agent_6 / 2000)" | bc -l)

# Extract and calculate for agent 7
agent_7=($(cat $file_name | grep ${agent[6]} | grep $graph | awk '{print $6}'))
agent_7=$(echo "scale=2; 1 - ($agent_7 / 2000)" | bc -l)

# Extract and calculate for agent 8
agent_8=($(cat $file_name | grep ${agent[7]} | grep $graph | awk '{print $6}'))
agent_8=$(echo "scale=2; 1 - ($agent_8 / 2000)" | bc -l)



agent_1_conf=0 agent_2_conf=0 agent_3_conf=0 agent_4_conf=0 agent_5_conf=0 agent_6_conf=0 agent_7_conf=0 agent_8_conf=0 






echo $1 $agent_1 $agent_1_conf  $agent_2 $agent_2_conf  $agent_3 $agent_3_conf $agent_4 $agent_4_conf $agent_5 $agent_5_conf $agent_6 $agent_6_conf  $agent_7 $agent_7_conf $agent_8 $agent_8_conf  >> ${graph}-attack-reach.res
echo 'EOD' >> ${graph}-attack-reach.res

# width=0.20
echo '
set boxwidth 0.17
plot $DATA using ($0):2:3 with boxerrorbars notitle  linecolor rgb gray, \
     $DATA using ($0+0.20):4:5 with boxerrorbars notitle  linecolor rgb brown1, \
     $DATA using ($0+0.40):6:7 with boxerrorbars notitle  linecolor rgb blue1, \
     $DATA using ($0+0.60):8:9 with boxerrorbars notitle  linecolor rgb blue2, \
     $DATA using ($0+0.80):10:11 with boxerrorbars notitle  linecolor rgb blue3, \
     $DATA using ($0+1.00):12:13 with boxerrorbars notitle  linecolor rgb green1, \
     $DATA using ($0+1.20):14:15 with boxerrorbars notitle linecolor rgb green2, \
     $DATA using ($0+1.40):16:17 with boxerrorbars notitle  linecolor rgb green3'>> ${graph}-attack-reach.res


gnuplot ${graph}-attack-reach.res

