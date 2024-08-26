# TelegramBots

TelegramBots机器人，目前只实现了贴纸下载和转换。文件转换需要使用FFmpeg

## 当前功能

当前功能：机器人将电报贴纸导出为webp或者webm，并使用FFmpeg进行格式转换

## 使用方法

点击 贴纸转换 按钮，发送单个贴纸或贴纸链接（类似于https://t.me/addstickers/AniColle），会在本机/服务器生成对应贴纸文件夹。后续会生成zip文件，提供下载链接

## 部署要求

- centos部署
- docker 
- net8
- ffmpeg
- 本机部署
- f5启动

## 安装步骤

1. git 克隆
2. 从@BotFather获取机器人令牌
3. 复制并根据需要appsettings.json编辑TelegramBotToken
