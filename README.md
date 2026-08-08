# Project Ember

<p align="center"><img src="https://raw.githubusercontent.com/Misanthropic-Wizards/Project-Ember/refs/heads/master/Resources/Textures/Logo/splashlogo.png" width="1024px" /></p>

---

Project Ember - хард форк билда [Einstein Engines](https://github.com/Simple-Station/Einstein-Engines) нацеленный на реализацию духа BayStation12 на базе Space Station 14, что предполагает менее хаотичный и более вдумчивый геймплей.

Space Station 14, очевидно, вдохновлён Space Station 13 и работает на движке [Robust Toolbox](https://github.com/space-wizards/RobustToolbox), что написан на C#.

На данный момент мы не запустили собственный сервер ввиду сырости самого проекта.

## Ссылки

[Website (отключён)](https://project-ember.space) | [Discord](placeholder) | [Steam(Space Station Beyond лаунчер)](https://store.steampowered.com/app/3731580/Space_Station_Beyond/) | [Steam(WizDen лаунчер)](https://store.steampowered.com/app/1255460/Space_Station_14/)

## Вклад в проект

Мы только рады помощи от любых заинтересованных людей.
У нас есть [список задач](placeholder) которые нужно было бы реализовать, и именно ты можешь нам помочь!

Важно: Если Вы используете ИИ для написания кода - ОБЯЗАТЕЛЬНО проверяйте весь написанный агентом код. Помните, инструмент должен находиться в прямых руках. Подробнее можно узнать в Discord.

## Компиляция

Ссылайтесь на [официальный гайд разработчиков игры](https://docs.spacestation14.com/en/general-development/setup/setting-up-a-development-environment.html) в настройке среды разработки, но держите в уме что многое у нас может отличаться.

### Завиисмости

> - Git
> - .NET SDK 9.0.101


### Windows

> 1. Склонируйте репозиторий.
> 2. Введите `git submodule update --init --recursive` в терминале для загрузки движка игры.
> 3. Запустите `Scripts/bat/buildAllDebug.bat` после любых изменений кода игры.
> 4. Запустите `Scripts/bat/runQuickAll.bat` для запуска клиента и сервера.
> 5. Подключитесь к localhost в запустившемся клиенте и наслаждайтесь игрой.

### Linux

> 1. Склонируйте репозиторий.
> 2. Введите `git submodule update --init --recursive` в терминале для загрузки движка игры.
> 3. Запустите `Scripts/sh/buildAllDebug.sh` после любых изменений кода игры.
> 4. Запустите `Scripts/sh/runQuickAll.sh` для запуска клиента и сервера.
> 5. Подключитесь к localhost в запустившемся клиенте и наслаждайтесь игрой.

### MacOS

> Вероятно, это точно так же, как на Linux. Я не проверял, апстрим, видимо, тоже.

## Лицензирование

Читайте [LEGAL.md](./LEGAL.md).
