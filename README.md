# Loxodon Framework Fody MethodBoundaryAspect.Fody plugin for Unity Engine!

This is a plugin for static weaving code that integrates MethodBoundaryAspect.Fody into a Unity project using the Loxodon Framework!

## INSTALLATION:

Add in UPM from git URL:
```
https://github.com/finerace/com.finerace.loxodon.fody.methodboundaryaspect.git
```

Paste this to your FodyWeavers.xml:

```
<?xml version="1.0" encoding="utf-8"?>
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <AssemblyNames>
    <Item>Assembly-CSharp</Item>
  </AssemblyNames>
  <MethodBoundaryAspect />
</Weavers>
```

Or just add this to your weavers list:

```
<MethodBoundaryAspect />
```

PROFIT!!
