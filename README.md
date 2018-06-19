# crt.sh-monitor
A monitor for crt.sh based on AWS Lambda

It may be called wiht the following syntax:

https://eo0kjkxapi.execute-api.eu-central-1.amazonaws.com/prod/crtsh-monitor?caID=52410&daystolookback=60&excluderevoked=false&excludeexpired=false&onlylinterrors=true&verbose=true

which gives as result:

`{
  "CAID": 52410,
  "ExcludeExpired": false,
  "OnlyLINTErrors": true,
  "DaysToLookBack": 60,
  "ExcludeRevoked": false,
  "Results": [
    {
      "CertificateID": "437210034",
      "SerialNumber": "YDJV6j2ysy4ycdTG6TB/D8W3dBU=",
      "SubjectDistinguishedName": "C=DE, ST=Bayern, L=Muenchen, O=Siemens, OU=Siemens Trust Center, CN=*.cfapps.industrycloud-staging.siemens.com",
      "NotBefore": "2018-05-03T06:40:18",
      "NotAfter": "2019-05-03T06:50:17",
      "FirstSeen": "2018-05-03T06:50:18.345",
      "Revoked": true,
      "Expired": false,
      "LintErrors": 2,
      "CrtSHLink": "https://crt.sh/?id=437210034&opt=cablint,x509lint,zlint"
    }
  ]
}`
