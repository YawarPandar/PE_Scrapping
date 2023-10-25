﻿using System;
using System.IO;
using System.Linq;
using PE_Scrapping.Tablas;
using Newtonsoft.Json.Linq;
using YPandar.Common.Functional;
using PE_Scrapping.Entidades;
using System.Collections.Generic;

namespace PE_Scrapping.Funciones
{
    public static class MainProcess
    {
        static InputParameters _input = new();

        static EndPointSet _endPointSet;
        static AppConfig _config;

        static string _etq_base;
        static string _appendName;

        static List<District> _dis = new();

        static List<District> TDistritos = new();
        static List<Province> TProvincias = new();
        static List<Department> TDepartamentos = new();

        static List<TUbigeo> TUbigeos = new();
        static List<TLocal> TLocales = new();
        static List<TMesa> TMesas = new();
        static List<TActa> TActas = new();
        static List<TVoto> TVotos = new();

        static Ubigeo _ubigeos;
        static List<Locale> _locales = new();
        static List<MesasVotacion> _mesas = new();
        static List<MesaDetalle> _mesaDetalles = new();

        public static void ExecuteProcess(InputParameters input)
        {
            _input = input;
            _config = Configuration.Initialize<AppConfig>();
            _endPointSet = input.ElectionType.Equals(Constants.ProcesarPrimeraV) ? _config.Api.First : _config.Api.Second;

            FunctionalHandler.WriteLines(new string[]
            {
                Messages.DOUBLE_LINE(),
                Messages.DOUBLE_LINE(),
                Messages.READING_DATA
            });
            ReadData();
            if (_config.SaveData) SaveData();
        }
        public static void ReadData()
        {
            ReadUbigeoData();
            FunctionalHandler.ExecuteActionIf(
                () =>
                {
                    _dis = TDistritos;
                    ReadElectionData();
                },
                () =>
                {
                    return _input.ProcessType.Equals(Constants.ProcesoTotal);
                },
                () =>
                {
                    FunctionalHandler.ExecuteActionIf(
                        () =>
                        {
                            ReadElectionDataByUbigeo();
                        },
                        () =>
                        {
                            return _input.PartialType.Equals(Constants.ProcesoUbigeo);
                        },
                        () =>
                        {
                            ReadElectionDataByTable();                                                        
                        }
                    );
                }
            );
        }
        private static void ReadUbigeoData()
        {
            _ubigeos = ReadApiData<Ubigeo>(_endPointSet.Ubigeo);
            TDistritos = _ubigeos.ubigeos.nacional.districts.Concat(_ubigeos.ubigeos.extranjero.states).OrderBy(o => o.CDGO_DIST).ToList();
            TProvincias = _ubigeos.ubigeos.nacional.provinces.Concat(_ubigeos.ubigeos.extranjero.countries).OrderBy(o => o.CDGO_PROV).ToList();
            TDepartamentos = _ubigeos.ubigeos.nacional.departments.Concat(_ubigeos.ubigeos.extranjero.continents).OrderBy(o => o.CDGO_DEP).ToList();
        }
        private static void ReadElectionData()
        {
            int actualItem = 0;
            _dis.ForEach(d =>
            {
                actualItem++;
                var pro_act = TProvincias.Where(p => p.CDGO_PROV.Equals(d.CDGO_PADRE)).FirstOrDefault();
                var dep_act = TDepartamentos.Where(d => d.CDGO_DEP.Equals(pro_act.CDGO_PADRE)).FirstOrDefault();
                _etq_base = Path.Combine(dep_act.DESC_DEP, pro_act.DESC_PROV, d.DESC_DIST);
                FunctionalHandler.WriteLines(new string[]
                {
                    string.Format(Messages.READING, _etq_base)
                });
                _appendName = string.Empty;
                Local locales = ReadApiData<Local>(_endPointSet.Locale.Replace(_config.Api.RequestParameters.UbigeoCode, d.CDGO_DIST));
                _locales.AddRange(locales.locales);
                locales.locales.ForEach(l =>
                {
                    FunctionalHandler.WriteLines(new string[]
                    {
                        string.Concat(Constants.TAB_KEY, l.TNOMB_LOCAL)
                    });
                    _appendName = FunctionalHandler.FormatFileName(l.TNOMB_LOCAL);
                    Mesa mesas = ReadApiData<Mesa>(_endPointSet.Table.Replace(_config.Api.RequestParameters.UbigeoCode, d.CDGO_DIST).Replace(_config.Api.RequestParameters.LocaleCode, l.CCODI_LOCAL));
                    _mesas.AddRange(mesas.mesasVotacion);
                    
                    mesas.mesasVotacion.ForEach(m =>
                    {
                        TMesas.Add(new TMesa { 
                            local_codigo = l.CCODI_LOCAL, local_ubigeo = l.CCODI_UBIGEO, mesa_numero = m.NUMMESA, mesa_imagen = m.IMAGEN, mesa_procesado = m.PROCESADO, eleccion = _input.ElectionType
                        });
                        FunctionalHandler.WriteLines(new string[]
                        {
                            string.Concat(Constants.TAB_KEY, Constants.TAB_KEY, m.NUMMESA)
                        });
                        _appendName = string.Concat(FunctionalHandler.FormatFileName(l.TNOMB_LOCAL), "_Mesa Nro ", m.NUMMESA);
                        MesaDetalle mesaDetalle = ReadApiData<MesaDetalle>(_endPointSet.TableDetail.Replace(_config.Api.RequestParameters.TableCode, m.NUMMESA));
                        _mesaDetalles.Add(mesaDetalle);

                        if (_config.DownloadFiles)
                        {
                            DescargarActas(mesaDetalle, m, l);                           
                        }

                    });
                });
                MostrarPorcentajeAvance(actualItem, _dis.Count, _etq_base);
            });
        }
        private static void ReadElectionDataByUbigeo()
        {
            _input.UbigeoCode = _input.UbigeoCode.Trim();
            FunctionalHandler.ExecuteActionIf(
                ()  =>
                {
                    _input.UbigeoCode = _input.UbigeoCode.EndsWith("0000") ? _input.UbigeoCode.Substring(0, 2) :
                        _input.UbigeoCode.EndsWith("00") ? _input.UbigeoCode.Substring(0, 4) : _input.UbigeoCode;
                    _dis = TDistritos.Where(o => o.CDGO_DIST.Contains(_input.UbigeoCode.Trim())).ToList();
                },
                () =>
                {
                    return _input.UbigeoCode.Length.Equals(6);
                },
                () =>
                {
                    FunctionalHandler.WriteLines(new string[]
                    {
                        Messages.DOUBLE_LINE(),
                        Messages.UBIGEO_NOT_FOUND,
                        Messages.DOUBLE_LINE()
                    });
                }
            );
            FunctionalHandler.ExecuteActionIf(
                () =>
                {
                    ReadElectionData();
                },
                () =>
                {
                    return _dis.Any();
                },
                () =>
                {
                    FunctionalHandler.WriteLines(new string[]
                    {
                        Messages.DOUBLE_LINE(),
                        Messages.UBIGEO_NOT_FOUND,
                        Messages.DOUBLE_LINE()
                    });
                }
            );
        }
        private static void ReadElectionDataByTable()
        {
            _etq_base = "Consulta_por_mesa";
            _appendName = string.Concat("_Mesa Nro ", _input.TableNumber);
            var tableDetail = ReadApiData<MesaDetalle>(_endPointSet.TableDetail.Replace(_config.Api.RequestParameters.TableCode, _input.TableNumber));
            FunctionalHandler.ExecuteActionIf(
                () =>
                {
                    FunctionalHandler.WriteLines(new string[]
                    {
                        string.Format(Messages.READING, string.Format(Messages.TABLE_NUMBER, _input.TableNumber))
                    });
                    _mesaDetalles.Add(tableDetail);
                    ReadApiData<Local>(_endPointSet.Locale.Replace(_config.Api.RequestParameters.UbigeoCode, tableDetail.procesos.generalPre.presidencial.CCODI_UBIGEO))
                    .locales.ForEach(l => {
                        if (!_mesas.Any())
                        {
                            MesasVotacion table = ReadApiData<Mesa>(
                                _endPointSet.Table
                                    .Replace(_config.Api.RequestParameters.UbigeoCode, tableDetail.procesos.generalPre.presidencial.CCODI_UBIGEO)
                                    .Replace(_config.Api.RequestParameters.LocaleCode, l.CCODI_LOCAL)
                                ).mesasVotacion.FirstOrDefault(m => m.NUMMESA.Equals(_input.TableNumber));
                            if (table != null)
                            {
                                _mesas.Add(table);
                                _locales.Add(l);

                                if(_config.DownloadFiles) DescargarActas(tableDetail, table, l);

                                TMesas.Add(new TMesa
                                {
                                    local_codigo = l.CCODI_LOCAL,
                                    local_ubigeo = l.CCODI_UBIGEO,
                                    mesa_numero = table.NUMMESA,
                                    mesa_imagen = table.IMAGEN,
                                    mesa_procesado = table.PROCESADO,
                                    eleccion = _input.ElectionType
                                });
                                if (tableDetail.procesos.generalPre != null)
                                {
                                    TActas.Add(new TActa
                                    {
                                        acta_imagen = tableDetail.procesos.generalPre.imageActa,
                                        acta_numero = tableDetail.procesos.generalPre.presidencial.CCOPIA_ACTA,
                                        habiles_numero = tableDetail.procesos.generalPre.presidencial.NNUME_HABILM,
                                        votantes_numero = tableDetail.procesos.generalPre.presidencial.TOT_CIUDADANOS_VOTARON,
                                        mesa_numero = table.NUMMESA,
                                        eleccion = _input.ElectionType,
                                        tipo_proceso = "PRE"
                                    });
                                    TVotos.AddRange(tableDetail.procesos.generalPre.votos.Select(v => new TVoto
                                    {
                                        mesa_numero = table.NUMMESA,
                                        acta_numero = tableDetail.procesos.generalPre.presidencial.CCOPIA_ACTA,
                                        auto_nombre = string.IsNullOrEmpty(v.AUTORIDAD) ? string.Empty : v.AUTORIDAD,
                                        lista_numero = string.IsNullOrEmpty(v.NLISTA) ? string.Empty : v.NLISTA,
                                        votos_total = v.congresal,
                                        eleccion = _input.ElectionType,
                                        tipo_proceso = "PRE"
                                    }));
                                }
                                if (tableDetail.procesos.generalCon != null)
                                {
                                    TActas.Add(new TActa
                                    {
                                        acta_imagen = tableDetail.procesos.generalCon.imageActa,
                                        acta_numero = tableDetail.procesos.generalCon.congresal.CCOPIA_ACTA,
                                        habiles_numero = tableDetail.procesos.generalCon.congresal.NNUME_HABILM,
                                        votantes_numero = tableDetail.procesos.generalCon.congresal.TOT_CIUDADANOS_VOTARON,
                                        mesa_numero = table.NUMMESA,
                                        eleccion = _input.ElectionType,
                                        tipo_proceso = "CON"
                                    });
                                    TVotos.AddRange(tableDetail.procesos.generalCon.votos.Select(v => new TVoto
                                    {
                                        mesa_numero = table.NUMMESA,
                                        acta_numero = tableDetail.procesos.generalCon.congresal.CCOPIA_ACTA,
                                        auto_nombre = string.IsNullOrEmpty(v.AUTORIDAD) ? string.Empty : v.AUTORIDAD,
                                        lista_numero = string.IsNullOrEmpty(v.NLISTA) ? string.Empty : v.NLISTA,
                                        votos_total = v.congresal,
                                        eleccion = _input.ElectionType,
                                        tipo_proceso = "CON"
                                    }));
                                }
                                if (tableDetail.procesos.generalPar != null)
                                {
                                    TActas.Add(new TActa
                                    {
                                        acta_imagen = tableDetail.procesos.generalPar.imageActa,
                                        acta_numero = tableDetail.procesos.generalPar.parlamento.CCOPIA_ACTA,
                                        habiles_numero = tableDetail.procesos.generalPar.parlamento.NNUME_HABILM,
                                        votantes_numero = tableDetail.procesos.generalPar.parlamento.TOT_CIUDADANOS_VOTARON,
                                        mesa_numero = table.NUMMESA,
                                        eleccion = _input.ElectionType,
                                        tipo_proceso = "PAR"
                                    });
                                    TVotos.AddRange(tableDetail.procesos.generalPar.votos.Select(v => new TVoto
                                    {
                                        mesa_numero = table.NUMMESA,
                                        acta_numero = tableDetail.procesos.generalPar.parlamento.CCOPIA_ACTA,
                                        auto_nombre = string.IsNullOrEmpty(v.AUTORIDAD) ? string.Empty : v.AUTORIDAD,
                                        lista_numero = string.IsNullOrEmpty(v.NLISTA) ? string.Empty : v.NLISTA,
                                        votos_total = v.congresal,
                                        eleccion = _input.ElectionType,
                                        tipo_proceso = "PAR"
                                    }));
                                }
                            }
                        }
                    });
                },
                () =>
                {
                    return tableDetail != null;
                },
                () =>
                {
                    FunctionalHandler.WriteLines(new string[]
                    {
                        Messages.DOUBLE_LINE(),
                        Messages.TABLE_NOT_FOUND,
                        Messages.DOUBLE_LINE()
                    });
                }
            );

        }
        private static T ReadApiData<T>(string method)
        {
            var json = HttpHandler.SendApiRequest(_endPointSet.BaseUri + method).Result;
            var pObject = JsonToObject<T>(json);
            if (_config.SaveJson) GuardarJSON(json, pObject.GetType().Name);
            return pObject;
        }        
        private static void GuardarJSON(string json, string nombre)
        {            
            var archivo_nombre = string.Concat(nombre.Trim(), "_", _appendName, _config.JsonFileExtension);
            var ruta_guardar = Path.Combine(_config.SavePath, _endPointSet.Title, "JSON", string.IsNullOrEmpty(_etq_base) ? string.Empty : _etq_base);
            if (!Directory.Exists(ruta_guardar)) Directory.CreateDirectory(ruta_guardar);
            File.WriteAllText(Path.Combine(ruta_guardar, archivo_nombre), json);
        }
        private static TValue JsonToObject<TValue>(string json)
        {
            JObject _jobject = JObject.Parse(json);
            TValue _object = _jobject.ToObject<TValue>();
            return _object;
        }
        private static void MostrarPorcentajeAvance(decimal index, decimal total, string etiqueta)
        {
            decimal avance = index * 100 / total;
            Console.Title = string.Format("{0}% - {1}", decimal.Round(avance, 2).ToString(), etiqueta);
        }
        private static void SaveData()
        {
            FormatDataToSave();
            DataConnection.ConnectionStart(_config.DataBaseName);
            FunctionalHandler.ExecuteActionIf(
                () =>
                {
                    DataConnection.PurgeAllData(_input.ElectionType);
                },
                () =>
                {
                    return _input.ProcessType.Equals(Constants.ProcesoTotal);
                },
                () =>
                {
                    DataConnection.PurgeUbigeoData(_input.ElectionType);
                    FunctionalHandler.ExecuteActionIf(
                        () =>
                        {
                            DataConnection.PurgeDataByUbigeo(_input.ElectionType, _input.UbigeoCode);
                        },
                        () =>
                        {
                            return _input.PartialType.Equals(Constants.ProcesoUbigeo);
                        },
                        () =>
                        {
                            DataConnection.PurgeDataByTable(_input.ElectionType, _input.TableNumber);
                        }
                    );
                }
            );
            DataConnection.SaveToTable(TUbigeos, "insert_ubigeo");
            DataConnection.SaveToTable(TLocales, "insert_local");
            DataConnection.SaveToTable(TMesas, "insert_mesa");
            DataConnection.SaveToTable(TActas, "insert_acta");
            DataConnection.SaveToTable(TVotos, "insert_voto");
            DataConnection.MergeWithMaster();
        }
        private static void FormatDataToSave()
        {
            TUbigeos.AddRange(
                TDepartamentos.Select(d =>
                new TUbigeo
                {
                    ubigeo_codigo = d.CDGO_DEP,
                    ubigeo_descripcion = d.DESC_DEP,
                    ubigeo_padre = d.CDGO_PADRE,
                    nivel = 1,
                    ambito = d.CDGO_DEP.StartsWith("9") ? "E" : "P",
                    eleccion = _input.ElectionType
                }).Concat(
                TProvincias.Select(d =>
                new TUbigeo
                {
                    ubigeo_codigo = d.CDGO_PROV,
                    ubigeo_descripcion = d.DESC_PROV,
                    ubigeo_padre = d.CDGO_PADRE,
                    nivel = 1,
                    ambito = d.CDGO_PROV.StartsWith("9") ? "E" : "P",
                    eleccion = _input.ElectionType
                }).Concat(
                TDistritos.Select(d =>
                new TUbigeo
                {
                    ubigeo_codigo = d.CDGO_DIST,
                    ubigeo_descripcion = d.DESC_DIST,
                    ubigeo_padre = d.CDGO_PADRE,
                    nivel = 1,
                    ambito = d.CDGO_DIST.StartsWith("9") ? "E" : "P",
                    eleccion = _input.ElectionType
                }))));
            TLocales = _locales.Select(l => new TLocal
            {
                local_codigo = l.CCODI_LOCAL,
                local_direccion = l.TDIRE_LOCAL,
                local_nombre = l.TNOMB_LOCAL,
                local_ubigeo = l.CCODI_UBIGEO,
                eleccion = _input.ElectionType
            }).ToList();
        }
        private static void DescargarActas(MesaDetalle mesaDetalle, MesasVotacion m, Locale l)
        {
            FunctionalHandler.ExecuteParallelActions(new List<Action>()
            {
                () =>
                {
                    if (mesaDetalle.procesos.generalPre != null)
                    {
                        _ = HttpHandler.DownloadFile(mesaDetalle.procesos.generalPre.imageActa.Replace(string.Format("{0}-actas-resultados-prod.s3.amazonaws.com", _endPointSet.FileStorage), string.Format(_config.FilePath, _endPointSet.Id)),
                            string.Concat("PRE-", m.NUMMESA, ".pdf"),
                                Path.Combine(_config.SavePath, _endPointSet.Title, "ACTAS", _etq_base),
                                FunctionalHandler.FormatFileName(l.TNOMB_LOCAL));
                    }
                },
                () =>
                {
                    if (mesaDetalle.procesos.generalCon != null)
                    {
                        _ = HttpHandler.DownloadFile(mesaDetalle.procesos.generalCon.imageActa.Replace(string.Format("{0}-actas-resultados-prod.s3.amazonaws.com", _endPointSet.FileStorage), string.Format(_config.FilePath, _endPointSet.Id)),
                            string.Concat("CON-", m.NUMMESA, ".pdf"), Path.Combine(_config.SavePath, "{0}"),
                                Path.Combine(_etq_base, FunctionalHandler.FormatFileName(l.TNOMB_LOCAL)));
                    }
                },
                () =>
                {
                    if (mesaDetalle.procesos.generalPar != null)
                    {
                        _ = HttpHandler.DownloadFile(mesaDetalle.procesos.generalPar.imageActa.Replace(string.Format("{0}-actas-resultados-prod.s3.amazonaws.com", _endPointSet.FileStorage), string.Format(_config.FilePath, _endPointSet.Id)),
                            string.Concat("PAR-", m.NUMMESA, ".pdf"), Path.Combine(_config.SavePath, "{0}"),
                                Path.Combine(_etq_base, FunctionalHandler.FormatFileName(l.TNOMB_LOCAL)));
                    }
                }
            });
        }
    }
}
