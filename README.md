<!-- TOC -->

- [기능](#%EA%B8%B0%EB%8A%A5)
- [제약사항](#%EC%A0%9C%EC%95%BD%EC%82%AC%ED%95%AD)
- [레퍼런스](#%EB%A0%88%ED%8D%BC%EB%9F%B0%EC%8A%A4)
- [CEF 추가 분석](#cef-%EC%B6%94%EA%B0%80-%EB%B6%84%EC%84%9D)
    - [CEF 간략소개](#cef-%EA%B0%84%EB%9E%B5%EC%86%8C%EA%B0%9C)
    - [CefSharp.Wpf 의 종속성](#cefsharpwpf-%EC%9D%98-%EC%A2%85%EC%86%8D%EC%84%B1)
    - [CEF hello world](#cef-hello-world)
    - [CEF 구조분석 예제](#cef-%EA%B5%AC%EC%A1%B0%EB%B6%84%EC%84%9D-%EC%98%88%EC%A0%9C)
    - [참고자료](#%EC%B0%B8%EA%B3%A0%EC%9E%90%EB%A3%8C)

<!-- /TOC -->

# 기능
- CEF(chromium .NET 버전)을 테스트
- 로컬html 로드
- javascript <-> c# 간의 상호통신

# 제약사항
- .NET 4.0 을 지원하기 위해서 CEF 버전을 49로 낮춤

# 레퍼런스
- https://www.nuget.org/packages/CefSharp.Wpf/49.0.0
- https://github.com/cefsharp/CefSharp/releases
    - CefSharp requires at least .Net 4.5.2 (Last version to support .Net 4.0 is 49)

# CEF 추가 분석

## CEF 간략소개
- Chromium Embedded Framework (CEF)는 Chromium 기반 브라우저를 다른 응용 프로그램에 임베드하기위한 간단한 프레임워크.
- CEF는 Chromium 을 사용하기 쉽게 C/C++ API 로 쉽게 사용할 수 있게 함.
- 기본으로 C/C++ 언어 지원이 되고, .NET, Java, Python, Go, Swift, Delphi 도 지원됨.
- CEF를 어플리케이션에 임베딩하면 아주 간단한 코드로 렌더링된 html과 javascript를 이용해서 통신 할 수도 있고,
- http통신, html렌더링, 쿠키, ... 등 브라우져에서 제공하는 다양한 기능에 접근해서 상세한 튜닝이 가능함.

## CefSharp.Wpf 의 종속성

```text
CEF(chromium embeded framework)
    libcef.dll, libEGL.dll ...
            ▲
            |
CefSharp(CEF .net binding)
    CefSharp.dll, CefSharp.Core.dll, CefSharp.BrowserSubprocess.Core.dll, ...
            ▲
            |
CefSharp.Wpf(CefSharp WPF binding)
    CefSharp.Wpf.dll
```

## CEF hello world

- build cefsimple
    - https://bitbucket.org/chromiumembedded/cef/src/master/tests/cefsimple/

- cefsimple.exe 실행

```powershell
# Load the local file “c:\example\example.html”
cefsimple.exe --url=file://c:/example/example.html
```

## CEF 구조분석 예제
- javascript -> c# 코드 호출은 어떻게 이뤄지는가? 
    - 코드가 너무 간단해서 궁금함...

```c#
var cbObj = new MyCallback();
this.Browser.RegisterJsObject("cbObj", cbObj);

public class MyCallback
{
    public void OnCallback(string param)
    {
        MessageBox.Show($"this is c# OnCallback function param : {param}");
    }
}
```

```javascript
cbObj.onCallback("hello c#");
```

- 아래와 같은 구조로 Cefsharp.Wpf 부터 chromium까지 코드가 흘러감.
    - 최종적으로 V8엔진에 콜백포인트가 저장되었다가 chromium이 javascript 이벤트 수신했을때 함수호출해 줌을 알수 있음.

```c#
////////////////////////////////////////////////////////////////////////////////
// c#에서 콜백수신할 객체 등록
////////////////////////////////////////////////////////////////////////////////

// 1. [c#] MyWpfApp
this.Browser.RegisterJsObject("cbObj", cbObj);

// 2. [c#] CefSharp.Wpf : ChromiumWebBrowser
public void RegisterJsObject(string name, object objectToBind, BindingOptions options = null)

// 3. [c++] CefSharp.Core : 
ManagedCefBrowserAdapter::RegisterJsObject(String^ name, Object^ object, BindingOptions^ options)

// 4. [c++] CefSharp.BrowserSubprocess.Core :
JavascriptCallback^ JavascriptCallbackRegistry::Register(const CefRefPtr<CefV8Context>& context, const CefRefPtr<CefV8Value>& value)
{
    Int64 newId = Interlocked::Increment(_lastId);
    JavascriptCallbackWrapper^ wrapper = gcnew JavascriptCallbackWrapper(value, context);
    _callbacks->TryAdd(newId, wrapper);

    auto result = gcnew JavascriptCallback();
    result->Id = newId;
    result->BrowserId = _browserId;
    result->FrameId = context->GetFrame()->GetIdentifier();
    return result;
}
```

```c#
// 5. html, javascript에서 발생한 이벤트를 chromium이 수신

// 6. [c++] CefSharp.BrowserSubprocess.Core
bool CefAppUnmanagedWrapper::OnProcessMessageReceived(CefRefPtr<CefBrowser> browser, CefProcessId sourceProcessId, CefRefPtr<CefProcessMessage> message)
{
    auto handled = false;
    auto name = message->GetName();
    auto argList = message->GetArgumentList();
    auto browserWrapper = FindBrowserWrapper(browser->GetIdentifier(), false);
    ...
    auto context = callbackWrapper->GetContext();
    auto value = callbackWrapper->GetValue();
    ...
    result = value->ExecuteFunction(nullptr, params);
    ...
    browser->SendProcessMessage(sourceProcessId, response);
    ...
}

// 7. [c++] libcef\renderer\v8_impl.cc
CefRefPtr<CefV8Value> CefV8ValueImpl::ExecuteFunction(
    CefRefPtr<CefV8Value> object,
    const CefV8ValueList& arguments) 
{
    // An empty context value defaults to the current context.
    CefRefPtr<CefV8Context> context;
    return ExecuteFunctionWithContext(context, object, arguments);
}

// 8. [c++] libcef\renderer\v8_impl.cc
CefRefPtr<CefV8Value> CefV8ValueImpl::ExecuteFunctionWithContext(
    CefRefPtr<CefV8Context> context,
    CefRefPtr<CefV8Value> object,
    const CefV8ValueList& arguments) 
{

    CEF_V8_REQUIRE_OBJECT_RETURN(NULL);

    v8::Isolate* isolate = handle_->isolate();
    v8::HandleScope handle_scope(isolate);
    v8::Local<v8::Value> value = handle_->GetNewV8Handle(false);

    v8::Local<v8::Object> obj = value->ToObject();
    v8::Local<v8::Function> func = v8::Local<v8::Function>::Cast(obj);
    v8::Local<v8::Object> recv;

    ...

    v8::MaybeLocal<v8::Value> func_rv = webkit_glue::CallV8Function(
        context_local, func, recv, argc, argv, handle_->isolate());

    ...
}

// 9. [c++] libcef\renderer\webkit_blue.cc
v8::MaybeLocal<v8::Value> CallV8Function(v8::Local<v8::Context> context,
                                         v8::Local<v8::Function> function,
                                         v8::Local<v8::Object> receiver,
                                         int argc,
                                         v8::Local<v8::Value> args[],
                                         v8::Isolate* isolate) {

    func_rv = blink::V8ScriptRunner::CallFunction(
        function, frame->GetDocument(), receiver, argc, args, isolate);

}
```

## 참고자료

- CEF 프로젝트
    - https://bitbucket.org/chromiumembedded/cef
- CEF 튜토리얼
    - https://bitbucket.org/chromiumembedded/cef/wiki/Tutorial
- CefSharp, CefSharp.WPF, CefSharp.Winform ... 프로젝트
    - https://github.com/cefsharp/CefSharp



